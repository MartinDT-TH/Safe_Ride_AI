using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Bookings.DTOs;
using SafeRide.Application.Features.Drivers.Services;
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
    private readonly IMatchingPolicyProvider _matchingPolicyProvider;
    private readonly IBookingLifecycleJobScheduler _jobScheduler;
    private readonly IMapRoutingService _mapRoutingService;
    private readonly DriverCompensationOptions _compensationOptions;

    public BookingMatchingService(
        ILogger<BookingMatchingService> logger,
        ApplicationDbContext dbContext,
        ILicenseCompatibilityService licenseCompatibilityService,
        IVehicleLicenseRequirementService vehicleLicenseRequirementService,
        IDateTimeProvider dateTimeProvider,
        IRedisService redisService,
        IRealtimeNotificationService realtimeNotificationService,
        IMatchingPolicyProvider matchingPolicyProvider,
        IBookingLifecycleJobScheduler jobScheduler,
        IMapRoutingService mapRoutingService,
        IOptions<DriverCompensationOptions> compensationOptions)
    {
        _logger = logger;
        _dbContext = dbContext;
        _licenseCompatibilityService = licenseCompatibilityService;
        _vehicleLicenseRequirementService = vehicleLicenseRequirementService;
        _dateTimeProvider = dateTimeProvider;
        _redisService = redisService;
        _realtimeNotificationService = realtimeNotificationService;
        _matchingPolicyProvider = matchingPolicyProvider;
        _jobScheduler = jobScheduler;
        _mapRoutingService = mapRoutingService;
        _compensationOptions = compensationOptions.Value;
    }

    public async Task<BookingDriverOfferDto?> StartMatchingAsync(
        long bookingId,
        CancellationToken cancellationToken)
    {
        var bookingLockKey = RedisKeys.MatchingBookingLock(bookingId);
        var bookingLockAcquired = false;
        try
        {
            var utcNow = _dateTimeProvider.UtcNow;
            bookingLockAcquired = await TryAcquireBookingLockAsync(bookingId);
            if (!bookingLockAcquired)
            {
                _logger.LogInformation(
                    "Matching skipped for booking {BookingId} because another matching attempt holds the booking lock.",
                    bookingId);
                return await GetActiveOfferDtoAsync(
                    bookingId,
                    utcNow,
                    cancellationToken);
            }

            // Flow: load booking and validate it is still searchable before candidate lookup.
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

            // Flow: enforce the matching window and cache matching state for client polling/retry jobs.
            var matchingSnapshot = _matchingPolicyProvider.GetSnapshot(booking, utcNow);
            if (matchingSnapshot.ExpiresAt.HasValue
                && utcNow >= matchingSnapshot.ExpiresAt.Value)
            {
                await ExpireBookingBecauseMatchingWindowExpiredAsync(
                    booking,
                    utcNow,
                    cancellationToken);

                _logger.LogInformation(
                    "Booking {BookingId} expired because matching window expired.",
                    bookingId);
                return null;
            }

            await CacheMatchingBookingAsync(booking, cancellationToken);

            // Flow: reuse an active offer instead of creating a duplicate for the same booking.
            var existingOffer = await GetActiveOfferDtoAsync(bookingId, utcNow, cancellationToken);
            if (existingOffer is not null)
            {
                return existingOffer;
            }

            if (DriverCompensationEligibility.ExceedsMaximumTripDistance(
                    booking,
                    _compensationOptions))
            {
                _logger.LogWarning(
                    "Matching rejected booking {BookingId}: immutable estimated distance {EstimatedDistanceKm} exceeds maximum {MaximumTripDistanceKm}.",
                    bookingId,
                    booking.EstimatedDistanceKm,
                    _compensationOptions.MaximumTripDistanceKm);
                return null;
            }

            var requiresLongDistanceOptIn =
                DriverCompensationEligibility.RequiresLongDistanceOptIn(
                    booking,
                    _compensationOptions);

            // Flow: seed candidates from Redis GEO, then validate live Redis status and DB license eligibility.
            var redisCandidateIds = await GetRedisCandidateDriverIdsAsync(
                booking.PickupLocation.X,
                booking.PickupLocation.Y,
                matchingSnapshot.CurrentSearchRadiusKm
                    ?? _matchingPolicyProvider.Current.InitialRadiusKm);
            if (redisCandidateIds.Count == 0)
            {
                _logger.LogInformation(
                    "Matching found no online Redis candidates inside the current radius for booking {BookingId}.",
                    bookingId);
                return null;
            }

            var approvedDriverLicensesRows = await _dbContext.DriverKycs
                .AsNoTracking()
                .Where(x =>
                    x.DocumentType == KycDocumentType.DRIVING_LICENSE &&
                    x.KycStatus == KycStatus.Approved &&
                    redisCandidateIds.Contains(x.DriverId))
                .Join(
                    _dbContext.DriverProfiles.AsNoTracking(),
                    kyc => kyc.DriverId,
                    profile => profile.DriverId,
                    (kyc, profile) => new
                    {
                        kyc.DriverId,
                        kyc.LicenseClass,
                        kyc.ExpiryDate,
                        kyc.VerifiedAt,
                        kyc.CreatedAt,
                        profile.WorkStatus,
                        profile.AcceptLongPickupTrips,
                        profile.AcceptLongDistanceTrips
                    })
                .Where(x => x.WorkStatus == DriverWorkStatus.Online)
                .ToListAsync(cancellationToken);

            var today = DateOnly.FromDateTime(utcNow);
            var approvedDriverLicenses = approvedDriverLicensesRows
                .Where(x => x.LicenseClass.HasValue
                    && (!x.ExpiryDate.HasValue || x.ExpiryDate.Value >= today))
                .Select(x => new
                {
                    x.DriverId,
                    LicenseClass = x.LicenseClass!.Value,
                    x.VerifiedAt,
                    x.CreatedAt,
                    x.WorkStatus,
                    x.AcceptLongPickupTrips,
                    x.AcceptLongDistanceTrips
                })
                .ToList();

            // Flow: build the blocked driver set from active trips, active offers, and drivers already tried.
            var activeDriverIds = await _dbContext.Trips
                .AsNoTracking()
                .Where(x => x.TripStatus == TripStatus.ACCEPTED
                    || x.TripStatus == TripStatus.DRIVER_ARRIVING
                    || x.TripStatus == TripStatus.ARRIVED
                    || x.TripStatus == TripStatus.IN_PROGRESS)
                .Where(x => redisCandidateIds.Contains(x.DriverId))
                .Select(x => x.DriverId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var blockedDriverIds = activeDriverIds.ToHashSet();
            var activeOfferDriverIds = await _dbContext.BookingDriverOffers
                .AsNoTracking()
                .Where(x => x.OfferStatus == DriverOfferStatus.Sent
                    || x.OfferStatus == DriverOfferStatus.DriverAccepted)
                .Where(x => x.ExpiresAt > utcNow)
                .Where(x => redisCandidateIds.Contains(x.DriverId))
                .Select(x => x.DriverId)
                .Distinct()
                .ToListAsync(cancellationToken);
            foreach (var driverId in activeOfferDriverIds)
            {
                blockedDriverIds.Add(driverId);
            }

            var previouslyOfferedDriverIds = await _dbContext.BookingDriverOffers
                .AsNoTracking()
                .Where(x => x.BookingId == bookingId)
                .Where(x => redisCandidateIds.Contains(x.DriverId))
                .Select(x => x.DriverId)
                .Distinct()
                .ToListAsync(cancellationToken);
            foreach (var driverId in previouslyOfferedDriverIds)
            {
                blockedDriverIds.Add(driverId);
            }

            var compatibleDriverIds = approvedDriverLicenses
                .GroupBy(x => x.DriverId)
                .Where(group => group.Any(x => _licenseCompatibilityService.CanDrive(
                    x.LicenseClass,
                    booking.Vehicle.RequiredLicenseClass)))
                .Select(group => group.Key)
                .ToHashSet();

            var driverPreferences = approvedDriverLicenses
                .GroupBy(x => x.DriverId)
                .ToDictionary(
                    group => group.Key,
                    group => new DriverMatchingPreferences(
                        group.First().AcceptLongPickupTrips,
                        group.First().AcceptLongDistanceTrips));

            var selfMatchedCount = 0;
            var eligibleDriverIds = new List<Guid>();
            // Preserve the nearest-first order returned by Redis GEO after SQL
            // license filtering. EF result order is not authoritative for routing.
            foreach (var driverId in redisCandidateIds)
            {
                if (!compatibleDriverIds.Contains(driverId))
                {
                    continue;
                }

                if (driverId == booking.CustomerId)
                {
                    selfMatchedCount++;
                    continue;
                }

                if (!blockedDriverIds.Contains(driverId)
                    && (!requiresLongDistanceOptIn
                        || driverPreferences[driverId].AcceptLongDistanceTrips))
                {
                    eligibleDriverIds.Add(driverId);
                }
            }

            // Redis GEO remains coarse discovery only. Route exactly one lock-held candidate at a time.
            Guid eligibleDriverId = Guid.Empty;
            PickupOfferSnapshot? pickupOfferSnapshot = null;
            foreach (var driverId in eligibleDriverIds)
            {
                if (!await TryAcquireDriverLockAsync(driverId, bookingId))
                {
                    continue;
                }

                var snapshot = await TryGetPickupOfferSnapshotAsync(
                    driverId,
                    booking,
                    cancellationToken);
                if (snapshot is null
                    || (DriverCompensationEligibility.RequiresLongPickupOptIn(
                            snapshot.PickupDistanceKm,
                            _compensationOptions)
                        && !driverPreferences[driverId].AcceptLongPickupTrips))
                {
                    await _redisService.RemoveAsync(RedisKeys.MatchingDriverLock(driverId));
                    continue;
                }

                eligibleDriverId = driverId;
                pickupOfferSnapshot = snapshot;
                break;
            }

            _logger.LogInformation(
                "Matching requested for booking {BookingId}. RedisCandidates={RedisCandidateCount}, ApprovedLicenseRows={ApprovedLicenseRows}, CompatibleDrivers={CompatibleDriverCount}, ActiveTripDrivers={ActiveTripDriverCount}, ActiveOfferDrivers={ActiveOfferDriverCount}, PreviouslyOfferedDrivers={PreviouslyOfferedDriverCount}, SelfMatchedCandidates={SelfMatchedCount}, EligibleDrivers={EligibleDriverCount}, DriverCandidateFound={HasCandidate}, RequiredLicense={RequiredLicenseClass}.",
                bookingId,
                redisCandidateIds.Count,
                approvedDriverLicenses.Count,
                compatibleDriverIds.Count,
                activeDriverIds.Count,
                activeOfferDriverIds.Count,
                previouslyOfferedDriverIds.Count,
                selfMatchedCount,
                eligibleDriverIds.Count,
                eligibleDriverId != Guid.Empty,
                booking.Vehicle.RequiredLicenseClass);

            if (eligibleDriverIds.Count > 0 && eligibleDriverId == Guid.Empty)
            {
                _logger.LogWarning(
                    "Matching found {EligibleDriverCount} eligible drivers for booking {BookingId}, but all matching locks were already held.",
                    eligibleDriverIds.Count,
                    bookingId);
            }

            if (eligibleDriverId == Guid.Empty)
            {
                return null;
            }

            var offerBeforeInsert = await GetActiveOfferDtoAsync(
                bookingId,
                utcNow,
                cancellationToken);
            if (offerBeforeInsert is not null)
            {
                return offerBeforeInsert;
            }

            // Flow: create one offer, schedule expiry, cache it, and notify only the matched driver/customer.
            var offer = new BookingDriverOffer
            {
                BookingId = bookingId,
                DriverId = eligibleDriverId,
                OfferStatus = DriverOfferStatus.Sent,
                OfferedAt = utcNow,
                ExpiresAt = utcNow.AddSeconds(_matchingPolicyProvider.Current.OfferExpireSeconds),
                PickupDistanceKm = pickupOfferSnapshot!.PickupDistanceKm,
                LongPickupCompensation = pickupOfferSnapshot.LongPickupCompensation
            };

            await _dbContext.BookingDriverOffers.AddAsync(offer, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await CacheMatchingOfferAsync(offer);
            _jobScheduler.ScheduleExpireDriverOffer(
                offer.Id,
                offer.ExpiresAt - utcNow);

            var offerDto = await GetActiveOfferDtoAsync(bookingId, utcNow, cancellationToken);
            if (offerDto is not null)
            {
                await _realtimeNotificationService.PublishDriverOfferReceivedAsync(
                    new DriverOfferReceivedEvent(
                        bookingId,
                        booking.CustomerId,
                        eligibleDriverId,
                        offerDto,
                        "Bạn có yêu cầu nhận chuyến mới từ SafeRide."),
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
        finally
        {
            if (bookingLockAcquired)
            {
                await _redisService.RemoveAsync(bookingLockKey);
            }
        }
    }

    private async Task CacheMatchingBookingAsync(
        Booking booking,
        CancellationToken cancellationToken)
    {
        var matchingStartedAt = _matchingPolicyProvider.GetMatchingStartedAt(booking)
            ?? _dateTimeProvider.UtcNow;
        var ttl = TimeSpan.FromMinutes(_matchingPolicyProvider.Current.BookingExpireAfterMinutes);
        var cache = new MatchingBookingCache(
            booking.BookingId,
            booking.CustomerId,
            booking.VehicleId,
            booking.Vehicle.RequiredLicenseClass,
            booking.PickupLocation.Y,
            booking.PickupLocation.X,
            matchingStartedAt);

        await _redisService.SetAsync(
            RedisKeys.MatchingBooking(booking.BookingId),
            JsonSerializer.Serialize(cache),
            ttl);
    }

    private async Task ExpireBookingBecauseMatchingWindowExpiredAsync(
        Booking booking,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (booking.BookingStatus != BookingStatus.Searching)
        {
            return;
        }

        booking.BookingStatus = BookingStatus.Expired;
        booking.UpdatedAt = utcNow;

        var openOffers = await _dbContext.BookingDriverOffers
            .Where(x => x.BookingId == booking.BookingId)
            .Where(x => x.OfferStatus == DriverOfferStatus.Sent
                || x.OfferStatus == DriverOfferStatus.DriverAccepted)
            .ToListAsync(cancellationToken);

        foreach (var offer in openOffers)
        {
            offer.OfferStatus = DriverOfferStatus.Expired;
            offer.ExpiredAt = utcNow;
            await _redisService.RemoveAsync(RedisKeys.MatchingOffer(offer.BookingId, offer.DriverId));
            await _redisService.RemoveAsync(RedisKeys.MatchingDriverLock(offer.DriverId));
            await _jobScheduler.CancelExpireDriverOfferAsync(offer.Id, cancellationToken);
        }

        await _redisService.RemoveAsync(RedisKeys.MatchingBooking(booking.BookingId));
        await _jobScheduler.CancelJobsForBookingAsync(booking.BookingId, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> GetRedisCandidateDriverIdsAsync(
        double pickupLongitude,
        double pickupLatitude,
        double radiusKm)
    {
        var members = await _redisService.GeoRadiusAsync(
            RedisKeys.OnlineDriversGeo,
            pickupLongitude,
            pickupLatitude,
            radiusKm,
            count: _matchingPolicyProvider.Current.CandidateLimit);

        if (members.Count == 0)
        {
            return [];
        }

        var candidateDriverIds = new List<Guid>();
        foreach (var member in members)
        {
            if (!Guid.TryParse(member, out var driverId))
            {
                continue;
            }

            candidateDriverIds.Add(driverId);
        }

        if (candidateDriverIds.Count == 0)
        {
            return [];
        }

        var redisKeys = new List<string>(candidateDriverIds.Count * 3);
        foreach (var driverId in candidateDriverIds)
        {
            redisKeys.Add(RedisKeys.DriverOnline(driverId));
            redisKeys.Add(RedisKeys.DriverStatus(driverId));
            redisKeys.Add(RedisKeys.DriverLocation(driverId));
        }

        var values = await _redisService.GetManyAsync(redisKeys);
        var driverIds = new List<Guid>();
        foreach (var driverId in candidateDriverIds)
        {
            values.TryGetValue(RedisKeys.DriverOnline(driverId), out var online);
            values.TryGetValue(RedisKeys.DriverStatus(driverId), out var status);
            values.TryGetValue(RedisKeys.DriverLocation(driverId), out var location);
            if (online is not null
                && location is not null
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
        return _redisService.TryAcquireDistributedLockAsync(
            RedisKeys.MatchingDriverLock(driverId),
            bookingId.ToString(),
            TimeSpan.FromSeconds(_matchingPolicyProvider.Current.OfferExpireSeconds));
    }

    private async Task<PickupOfferSnapshot?> TryGetPickupOfferSnapshotAsync(
        Guid driverId,
        Booking booking,
        CancellationToken cancellationToken)
    {
        var locationJson = await _redisService.GetAsync(RedisKeys.DriverLocation(driverId));
        if (string.IsNullOrWhiteSpace(locationJson))
        {
            return null;
        }

        DriverLocationCache? driverLocation;
        try
        {
            driverLocation = JsonSerializer.Deserialize<DriverLocationCache>(locationJson);
        }
        catch (JsonException)
        {
            await _redisService.RemoveAsync(RedisKeys.DriverLocation(driverId));
            return null;
        }

        if (driverLocation is null || booking.PickupLocation is null)
        {
            return null;
        }

        try
        {
            var route = await _mapRoutingService.GetRouteEstimateAsync(
                new RouteEstimateRequest
                {
                    Origin = new LocationPoint(driverLocation.Latitude, driverLocation.Longitude),
                    Destination = new LocationPoint(booking.PickupLocation.Y, booking.PickupLocation.X),
                    Provider = MapProvider.Auto,
                    TravelMode = MapTravelMode.Car,
                    IncludePolyline = false,
                    RequestSource = "DriverMatchingPickupEligibility"
                },
                cancellationToken);

            if (double.IsNaN(route.DistanceMeters)
                || double.IsInfinity(route.DistanceMeters)
                || route.DistanceMeters < 0)
            {
                return null;
            }

            var pickupDistanceKm = decimal.Round((decimal)(route.DistanceMeters / 1000d), 3);
            return new PickupOfferSnapshot(
                pickupDistanceKm,
                DriverCompensationEligibility.CalculateLongPickupCompensation(
                    pickupDistanceKm,
                    _compensationOptions));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Authoritative pickup route failed for driver {DriverId} and booking {BookingId}; candidate was skipped.",
                driverId,
                booking.BookingId);
            return null;
        }
    }

    private Task<bool> TryAcquireBookingLockAsync(long bookingId)
    {
        var options = _matchingPolicyProvider.Current;
        var ttlSeconds = Math.Max(
            options.OfferExpireSeconds,
            options.MatchingTickSeconds * 2);
        ttlSeconds = Math.Max(ttlSeconds, 30);

        return _redisService.TryAcquireDistributedLockAsync(
            RedisKeys.MatchingBookingLock(bookingId),
            bookingId.ToString(),
            TimeSpan.FromSeconds(ttlSeconds));
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

    private async Task<BookingDriverOfferDto?> GetActiveOfferDtoAsync(
        long bookingId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var activeOfferRows = await (
            from driverOffer in _dbContext.BookingDriverOffers.AsNoTracking()
            join profile in _dbContext.DriverProfiles.AsNoTracking()
                on driverOffer.DriverId equals profile.DriverId
            join user in _dbContext.AspNetUsers.AsNoTracking()
                on driverOffer.DriverId equals user.Id
            join kyc in _dbContext.DriverKycs.AsNoTracking()
                on driverOffer.DriverId equals kyc.DriverId
            where driverOffer.BookingId == bookingId
                && (driverOffer.OfferStatus == DriverOfferStatus.Sent
                    || driverOffer.OfferStatus == DriverOfferStatus.DriverAccepted)
                && driverOffer.ExpiresAt > utcNow
                && kyc.DocumentType == KycDocumentType.DRIVING_LICENSE
                && kyc.KycStatus == KycStatus.Approved
            orderby kyc.VerifiedAt ?? kyc.CreatedAt descending
            select new
            {
                driverOffer.Id,
                driverOffer.DriverId,
                user.FullName,
                user.UserName,
                user.AvatarUrl,
                profile.ExperienceYears,
                LicenseClass = kyc.LicenseClass,
                kyc.ExpiryDate,
                driverOffer.ExpiresAt,
                driverOffer.OfferStatus
            })
            .ToListAsync(cancellationToken);
        var today = DateOnly.FromDateTime(utcNow);
        var activeOffer = activeOfferRows.FirstOrDefault(x =>
            x.LicenseClass.HasValue
            && (!x.ExpiryDate.HasValue || x.ExpiryDate.Value >= today));

        if (activeOffer is null)
        {
            return null;
        }
        var activeOfferLicenseClass = activeOffer.LicenseClass.GetValueOrDefault();

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
            activeOfferLicenseClass,
            activeOffer.ExpiresAt,
            activeOffer.OfferStatus,
            activeOffer.OfferStatus == DriverOfferStatus.DriverAccepted
                ? (int?)Math.Max(0, (int)Math.Ceiling((activeOffer.ExpiresAt - utcNow).TotalSeconds))
                : null);
    }

}

internal sealed record DriverMatchingPreferences(
    bool AcceptLongPickupTrips,
    bool AcceptLongDistanceTrips);

internal sealed record PickupOfferSnapshot(
    decimal PickupDistanceKm,
    decimal LongPickupCompensation);
