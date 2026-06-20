using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Bookings.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using System.Text.Json;

namespace SafeRide.Infrastructure.Services;

public sealed class BookingMatchingService : IBookingMatchingService
{
    private readonly ILogger<BookingMatchingService> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILicenseCompatibilityService _licenseCompatibilityService;
    private readonly IVehicleLicenseRequirementService _vehicleLicenseRequirementService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IRedisService _redisService;
    private readonly IRealtimeNotificationService _realtimeNotificationService;

    public BookingMatchingService(
        ILogger<BookingMatchingService> logger,
        ApplicationDbContext dbContext,
        ILicenseCompatibilityService licenseCompatibilityService,
        IVehicleLicenseRequirementService vehicleLicenseRequirementService,
        IDateTimeProvider dateTimeProvider,
        IRedisService redisService,
        IRealtimeNotificationService realtimeNotificationService)
    {
        _logger = logger;
        _dbContext = dbContext;
        _licenseCompatibilityService = licenseCompatibilityService;
        _vehicleLicenseRequirementService = vehicleLicenseRequirementService;
        _dateTimeProvider = dateTimeProvider;
        _redisService = redisService;
        _realtimeNotificationService = realtimeNotificationService;
    }

    public async Task<BookingDriverOfferDto?> StartMatchingAsync(
        long bookingId,
        CancellationToken cancellationToken)
    {
        try
        {
            var utcNow = _dateTimeProvider.UtcNow;
            var booking = await _dbContext.Bookings
                .Include(x => x.Vehicle)
                .FirstOrDefaultAsync(
                    x => x.BookingId == bookingId,
                    cancellationToken);
            if (booking is null)
            {
                _logger.LogWarning(
                    "Matching skipped because booking {BookingId} was not found.",
                    bookingId);
                return null;
            }

            if (booking.PickupLocation == null)
            {
                _logger.LogError(
                    "Matching failed: PickupLocation is null for booking {BookingId}.",
                    bookingId);
                return null;
            }

            if (booking.BookingStatus != BookingStatus.Searching)
            {
                _logger.LogInformation(
                    "Matching skipped for booking {BookingId} because status is {BookingStatus}.",
                    bookingId,
                    booking.BookingStatus);
                return null;
            }

            if (booking.Vehicle == null)
            {
                _logger.LogError(
                    "Matching failed: Vehicle is null for booking {BookingId}.",
                    bookingId);
                return null;
            }

            if (!_vehicleLicenseRequirementService.HasValidRequirement(booking.Vehicle))
            {
                _logger.LogWarning(
                    "Matching skipped for booking {BookingId} because vehicle {VehicleId} has invalid license requirement.",
                    bookingId,
                    booking.VehicleId);
                return null;
            }

            await CacheMatchingBookingAsync(booking, utcNow, cancellationToken);
            await ExpireStaleOffersAsync(utcNow, cancellationToken);

            var existingOffer = await GetActiveOfferDtoAsync(bookingId, utcNow, cancellationToken);
            if (existingOffer is not null)
            {
                return existingOffer;
            }

            var redisCandidateIds = await GetRedisCandidateDriverIdsAsync(
                booking.PickupLocation.X,
                booking.PickupLocation.Y);

        var approvedDriverLicensesQuery = _dbContext.DriverKycs
            .AsNoTracking()
            .Where(x =>
                x.DocumentType == KycDocumentType.DRIVING_LICENSE &&
                x.KycStatus == KycStatus.Approved &&
                x.LicenseClass.HasValue)
            .Join(
                _dbContext.DriverProfiles
                    .AsNoTracking()
                    .Where(x => x.WorkStatus == DriverWorkStatus.Online),
                kyc => kyc.DriverId,
                profile => profile.DriverId,
                (kyc, profile) => new
                {
                    kyc.DriverId,
                    LicenseClass = kyc.LicenseClass!.Value,
                    kyc.VerifiedAt,
                    kyc.CreatedAt
                });

        if (redisCandidateIds.Count > 0)
        {
            approvedDriverLicensesQuery = approvedDriverLicensesQuery
                .Where(x => redisCandidateIds.Contains(x.DriverId));
        }

        var approvedDriverLicenses = await approvedDriverLicensesQuery
            .ToListAsync(cancellationToken);

        var activeDriverIds = await _dbContext.Trips
            .AsNoTracking()
            .Where(x => x.TripStatus == TripStatus.ACCEPTED
                || x.TripStatus == TripStatus.DRIVER_ARRIVING
                || x.TripStatus == TripStatus.ARRIVED
                || x.TripStatus == TripStatus.IN_PROGRESS)
            .Select(x => x.DriverId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var blockedDriverIds = activeDriverIds.ToHashSet();
        var previouslyOfferedDriverIds = await _dbContext.BookingDriverOffers
            .AsNoTracking()
            .Where(x => x.BookingId == bookingId)
            .Select(x => x.DriverId)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var driverId in previouslyOfferedDriverIds)
        {
            blockedDriverIds.Add(driverId);
        }

        var eligibleDriverIds = approvedDriverLicenses
            .GroupBy(x => x.DriverId)
            .Select(group => group
                .OrderByDescending(x => x.VerifiedAt ?? x.CreatedAt)
                .First())
            .Where(x => _licenseCompatibilityService.CanDrive(
                x.LicenseClass,
                booking.Vehicle.RequiredLicenseClass))
            .Where(x => !blockedDriverIds.Contains(x.DriverId))
            .Select(x => x.DriverId)
            .ToList();

        Guid eligibleDriverId = Guid.Empty;
        foreach (var driverId in eligibleDriverIds)
        {
            if (await TryAcquireDriverLockAsync(driverId, bookingId))
            {
                eligibleDriverId = driverId;
                break;
            }
        }

        _logger.LogInformation(
            "Matching requested for booking {BookingId}. Driver candidate found: {HasCandidate}. Required license: {RequiredLicenseClass}.",
            bookingId,
            eligibleDriverId != Guid.Empty,
            booking.Vehicle.RequiredLicenseClass);

            if (eligibleDriverId == Guid.Empty)
            {
                return null;
            }

            var offer = new BookingDriverOffer
            {
                BookingId = bookingId,
                DriverId = eligibleDriverId,
                OfferStatus = DriverOfferStatus.Offered,
                OfferedAt = utcNow,
                ExpiresAt = utcNow.AddMinutes(2)
            };

            await _dbContext.BookingDriverOffers.AddAsync(offer, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await CacheMatchingOfferAsync(offer);

            var offerDto = await GetActiveOfferDtoAsync(bookingId, utcNow, cancellationToken);
            if (offerDto is not null)
            {
                await _realtimeNotificationService.PublishDriverOfferCreatedAsync(
                    new DriverOfferCreatedEvent(
                        bookingId,
                        booking.CustomerId,
                        offerDto),
                    cancellationToken);
                await _realtimeNotificationService.PublishDriverMatchedAsync(
                    new DriverMatchedEvent(
                        bookingId,
                        eligibleDriverId,
                        offer.OfferedAt,
                        offer.ExpiresAt),
                    cancellationToken);
            }

            return offerDto;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Error during matching process for booking {BookingId}.",
                bookingId);
            return null;
        }
    }

    private async Task CacheMatchingBookingAsync(
        Booking booking,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var cache = new MatchingBookingCache(
            booking.BookingId,
            booking.CustomerId,
            booking.VehicleId,
            booking.Vehicle.RequiredLicenseClass,
            booking.PickupLocation.Y,
            booking.PickupLocation.X,
            utcNow);

        await _redisService.SetAsync(
            RedisKeys.MatchingBooking(booking.BookingId),
            JsonSerializer.Serialize(cache),
            TimeSpan.FromMinutes(10));
    }

    private async Task<IReadOnlyList<Guid>> GetRedisCandidateDriverIdsAsync(
        double pickupLongitude,
        double pickupLatitude)
    {
        var members = await _redisService.GeoRadiusAsync(
            RedisKeys.OnlineDriversGeo,
            pickupLongitude,
            pickupLatitude,
            radiusKm: 10,
            count: 20);

        if (members.Count == 0)
        {
            return [];
        }

        var driverIds = new List<Guid>();
        foreach (var member in members)
        {
            if (!Guid.TryParse(member, out var driverId))
            {
                continue;
            }

            var online = await _redisService.GetAsync(
                RedisKeys.DriverOnline(driverId));
            var status = await _redisService.GetAsync(
                RedisKeys.DriverStatus(driverId));
            if (online is not null
                && string.Equals(
                    status,
                    DriverWorkStatus.Online.ToString(),
                    StringComparison.OrdinalIgnoreCase))
            {
                driverIds.Add(driverId);
            }
        }

        return driverIds;
    }

    private Task<bool> TryAcquireDriverLockAsync(
        Guid driverId,
        long bookingId)
    {
        return _redisService.SetIfNotExistsAsync(
            RedisKeys.MatchingDriverLock(driverId),
            bookingId.ToString(),
            TimeSpan.FromMinutes(2));
    }

    private Task CacheMatchingOfferAsync(BookingDriverOffer offer)
    {
        var cache = new MatchingOfferCache(
            offer.BookingId,
            offer.Id,
            offer.DriverId,
            offer.OfferedAt,
            offer.ExpiresAt);

        return _redisService.SetAsync(
            RedisKeys.MatchingOffer(offer.BookingId, offer.DriverId),
            JsonSerializer.Serialize(cache),
            offer.ExpiresAt - offer.OfferedAt);
    }

    private async Task ExpireStaleOffersAsync(
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var staleOffers = await _dbContext.BookingDriverOffers
            .Where(x => x.OfferStatus == DriverOfferStatus.Offered
                && x.ExpiresAt <= utcNow)
            .ToListAsync(cancellationToken);

        foreach (var offer in staleOffers)
        {
            offer.OfferStatus = DriverOfferStatus.Expired;
            offer.ExpiredAt = utcNow;
            await _redisService.RemoveAsync(
                RedisKeys.MatchingOffer(offer.BookingId, offer.DriverId));
            await _redisService.RemoveAsync(
                RedisKeys.MatchingDriverLock(offer.DriverId));
        }

        if (staleOffers.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<BookingDriverOfferDto?> GetActiveOfferDtoAsync(
        long bookingId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var activeOffer = await (
            from driverOffer in _dbContext.BookingDriverOffers.AsNoTracking()
            join profile in _dbContext.DriverProfiles.AsNoTracking()
                on driverOffer.DriverId equals profile.DriverId
            join user in _dbContext.AspNetUsers.AsNoTracking()
                on driverOffer.DriverId equals user.Id
            join kyc in _dbContext.DriverKycs.AsNoTracking()
                on driverOffer.DriverId equals kyc.DriverId
            where driverOffer.BookingId == bookingId
                && driverOffer.OfferStatus == DriverOfferStatus.Offered
                && driverOffer.ExpiresAt > utcNow
                && kyc.DocumentType == KycDocumentType.DRIVING_LICENSE
                && kyc.KycStatus == KycStatus.Approved
                && kyc.LicenseClass.HasValue
            orderby kyc.VerifiedAt ?? kyc.CreatedAt descending
            select new
            {
                driverOffer.Id,
                driverOffer.DriverId,
                user.FullName,
                user.UserName,
                user.AvatarUrl,
                profile.ExperienceYears,
                LicenseClass = kyc.LicenseClass ?? LicenseClass.A1,
                driverOffer.ExpiresAt
            })
            .Take(1)
            .FirstOrDefaultAsync(cancellationToken);

        if (activeOffer is null)
        {
            return null;
        }

        var ratingStats = await _dbContext.Ratings
            .AsNoTracking()
            .Where(x => x.DriverId == activeOffer.DriverId)
            .GroupBy(x => x.DriverId)
            .Select(group => new
            {
                AverageRating = group.Average(x => (double)x.RatingScore),
                TripCount = group.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new BookingDriverOfferDto(
            activeOffer.Id,
            activeOffer.DriverId,
            activeOffer.FullName ?? activeOffer.UserName ?? "Tài xế SafeRide",
            activeOffer.AvatarUrl,
            ratingStats is null ? 0 : Math.Round(ratingStats.AverageRating, 1),
            ratingStats?.TripCount ?? 0,
            activeOffer.ExperienceYears ?? 0,
            activeOffer.LicenseClass,
            activeOffer.ExpiresAt);
    }

    private static bool IsActiveTripStatus(TripStatus status)
    {
        return status is TripStatus.ACCEPTED
            or TripStatus.DRIVER_ARRIVING
            or TripStatus.ARRIVED
            or TripStatus.IN_PROGRESS;
    }
}
