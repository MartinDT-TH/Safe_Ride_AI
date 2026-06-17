using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class BookingMatchingService : IBookingMatchingService
{
    private readonly ILogger<BookingMatchingService> _logger;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILicenseCompatibilityService _licenseCompatibilityService;
    private readonly IVehicleLicenseRequirementService _vehicleLicenseRequirementService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BookingMatchingService(
        ILogger<BookingMatchingService> logger,
        ApplicationDbContext dbContext,
        ILicenseCompatibilityService licenseCompatibilityService,
        IVehicleLicenseRequirementService vehicleLicenseRequirementService,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dbContext = dbContext;
        _licenseCompatibilityService = licenseCompatibilityService;
        _vehicleLicenseRequirementService = vehicleLicenseRequirementService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<BookingDriverOfferDto?> StartMatchingAsync(
        long bookingId,
        CancellationToken cancellationToken)
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

        if (booking.BookingStatus != BookingStatus.Searching)
        {
            _logger.LogInformation(
                "Matching skipped for booking {BookingId} because status is {BookingStatus}.",
                bookingId,
                booking.BookingStatus);
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

        await ExpireStaleOffersAsync(utcNow, cancellationToken);

        var existingOffer = await GetActiveOfferDtoAsync(bookingId, utcNow, cancellationToken);
        if (existingOffer is not null)
        {
            return existingOffer;
        }

        var approvedDriverLicenses = await _dbContext.DriverKycs
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
                })
            .ToListAsync(cancellationToken);

        var activeDriverIds = await _dbContext.Trips
            .AsNoTracking()
            .Where(x => IsActiveTripStatus(x.TripStatus))
            .Select(x => x.DriverId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var blockedDriverIds = activeDriverIds.ToHashSet();

        var eligibleDriverId = approvedDriverLicenses
            .GroupBy(x => x.DriverId)
            .Select(group => group
                .OrderByDescending(x => x.VerifiedAt ?? x.CreatedAt)
                .First())
            .Where(x => _licenseCompatibilityService.CanDrive(
                x.LicenseClass,
                booking.Vehicle.RequiredLicenseClass))
            .Where(x => !blockedDriverIds.Contains(x.DriverId))
            .Select(x => x.DriverId)
            .FirstOrDefault();

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

        return await GetActiveOfferDtoAsync(bookingId, utcNow, cancellationToken);
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
                LicenseClass = kyc.LicenseClass!.Value,
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
                Rating = Math.Round(group.Average(x => x.RatingScore), 1),
                TripCount = group.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new BookingDriverOfferDto(
            activeOffer.Id,
            activeOffer.DriverId,
            activeOffer.FullName ?? activeOffer.UserName ?? "Tài xế SafeRide",
            activeOffer.AvatarUrl,
            ratingStats?.Rating ?? 0,
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
