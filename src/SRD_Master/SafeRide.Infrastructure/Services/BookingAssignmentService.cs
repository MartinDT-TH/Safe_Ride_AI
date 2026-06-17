using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.Bookings.Commands.CreateBooking;
using SafeRide.Application.Features.Bookings.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class BookingAssignmentService : IBookingAssignmentService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILicenseCompatibilityService _licenseCompatibilityService;
    private readonly IVehicleLicenseRequirementService _vehicleLicenseRequirementService;

    public BookingAssignmentService(
        ApplicationDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ILicenseCompatibilityService licenseCompatibilityService,
        IVehicleLicenseRequirementService vehicleLicenseRequirementService)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _licenseCompatibilityService = licenseCompatibilityService;
        _vehicleLicenseRequirementService = vehicleLicenseRequirementService;
    }

    public async Task<CreateBookingResponse> ConfirmDriverAsync(
        Guid customerId,
        long bookingId,
        CancellationToken cancellationToken)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var booking = await _dbContext.Bookings
            .Include(x => x.Vehicle)
            .FirstOrDefaultAsync(
                x => x.BookingId == bookingId && x.CustomerId == customerId,
                cancellationToken);
        if (booking is null)
        {
            throw new BookingException(
                "booking.not_found",
                "Không tìm thấy chuyến của bạn.",
                404);
        }

        if (booking.BookingStatus != BookingStatus.Searching)
        {
            throw new BookingException(
                "booking.cannot_confirm_driver",
                "Chuyến này không còn ở trạng thái tìm tài xế.",
                409);
        }

        if (!_vehicleLicenseRequirementService.HasValidRequirement(booking.Vehicle))
        {
            throw new BookingException(
                "booking.invalid_vehicle_license_requirement",
                "Không xác định được hạng bằng lái cần thiết cho xe đã chọn.",
                400);
        }

        var offer = await _dbContext.BookingDriverOffers
            .Where(x => x.BookingId == bookingId
                && x.OfferStatus == DriverOfferStatus.Offered)
            .OrderByDescending(x => x.OfferedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (offer is null)
        {
            throw new BookingException(
                "booking.driver_offer_not_found",
                "Chưa có tài xế phù hợp để xác nhận.",
                409);
        }

        if (offer.ExpiresAt <= utcNow)
        {
            offer.OfferStatus = DriverOfferStatus.Expired;
            offer.ExpiredAt = utcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            throw new BookingException(
                "booking.driver_offer_expired",
                "Tài xế được đề xuất đã hết thời gian giữ chỗ. Vui lòng tìm lại.",
                409);
        }

        var driverProfile = await _dbContext.DriverProfiles
            .FirstOrDefaultAsync(
                x => x.DriverId == offer.DriverId,
                cancellationToken);
        if (driverProfile is null || driverProfile.WorkStatus != DriverWorkStatus.Online)
        {
            throw new BookingException(
                "booking.driver_unavailable",
                "Tài xế không còn sẵn sàng. Vui lòng tìm lại.",
                409);
        }

        var driverLicense = await _dbContext.DriverKycs
            .AsNoTracking()
            .Where(x => x.DriverId == offer.DriverId
                && x.DocumentType == KycDocumentType.DRIVING_LICENSE
                && x.KycStatus == KycStatus.Approved
                && x.LicenseClass.HasValue)
            .OrderByDescending(x => x.VerifiedAt ?? x.CreatedAt)
            .Select(x => x.LicenseClass)
            .FirstOrDefaultAsync(cancellationToken);
        if (!driverLicense.HasValue
            || !_licenseCompatibilityService.CanDrive(
                driverLicense.Value,
                booking.Vehicle.RequiredLicenseClass))
        {
            throw new BookingException(
                "booking.driver_license_not_compatible",
                "Bằng lái của tài xế không phù hợp với xe đã chọn.",
                409);
        }

        var hasActiveTrip = await _dbContext.Trips
            .AnyAsync(
                x => x.DriverId == offer.DriverId
                    && IsActiveTripStatus(x.TripStatus),
                cancellationToken);
        if (hasActiveTrip)
        {
            throw new BookingException(
                "booking.driver_busy",
                "Tài xế vừa nhận chuyến khác. Vui lòng tìm lại.",
                409);
        }

        var trip = new Trip
        {
            BookingId = booking.BookingId,
            DriverId = offer.DriverId,
            TripStatus = TripStatus.ACCEPTED,
            DriverAssignedAt = utcNow,
            RoutePolyline = booking.RoutePolyline,
            CreatedAt = utcNow
        };

        offer.OfferStatus = DriverOfferStatus.Confirmed;
        offer.ConfirmedAt = utcNow;
        booking.BookingStatus = BookingStatus.DriverAssigned;
        booking.UpdatedAt = utcNow;
        driverProfile.WorkStatus = DriverWorkStatus.Busy;
        driverProfile.UpdatedAt = utcNow;

        await _dbContext.Trips.AddAsync(trip, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var driverOffer = await GetOfferDtoAsync(offer.Id, cancellationToken);

        return new CreateBookingResponse(
            booking.BookingId,
            booking.BookingType,
            booking.BookingStatus,
            booking.ScheduledAt,
            (double)(booking.EstimatedDistanceKm ?? 0m),
            booking.EstimatedDurationMinutes ?? 0,
            booking.EstimatedFare,
            booking.RoutePolyline,
            "Đã xác nhận tài xế cho chuyến đi.",
            driverOffer);
    }

    private async Task<BookingDriverOfferDto?> GetOfferDtoAsync(
        long offerId,
        CancellationToken cancellationToken)
    {
        var confirmedOffer = await (
            from driverOffer in _dbContext.BookingDriverOffers.AsNoTracking()
            join profile in _dbContext.DriverProfiles.AsNoTracking()
                on driverOffer.DriverId equals profile.DriverId
            join user in _dbContext.AspNetUsers.AsNoTracking()
                on driverOffer.DriverId equals user.Id
            join kyc in _dbContext.DriverKycs.AsNoTracking()
                on driverOffer.DriverId equals kyc.DriverId
            where driverOffer.Id == offerId
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

        if (confirmedOffer is null)
        {
            return null;
        }

        var ratingStats = await _dbContext.Ratings
            .AsNoTracking()
            .Where(x => x.DriverId == confirmedOffer.DriverId)
            .GroupBy(x => x.DriverId)
            .Select(group => new
            {
                Rating = Math.Round(group.Average(x => x.RatingScore), 1),
                TripCount = group.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new BookingDriverOfferDto(
            confirmedOffer.Id,
            confirmedOffer.DriverId,
            confirmedOffer.FullName ?? confirmedOffer.UserName ?? "Tài xế SafeRide",
            confirmedOffer.AvatarUrl,
            ratingStats?.Rating ?? 0,
            ratingStats?.TripCount ?? 0,
            confirmedOffer.ExperienceYears ?? 0,
            confirmedOffer.LicenseClass,
            confirmedOffer.ExpiresAt);
    }

    private static bool IsActiveTripStatus(TripStatus status)
    {
        return status is TripStatus.ACCEPTED
            or TripStatus.DRIVER_ARRIVING
            or TripStatus.ARRIVED
            or TripStatus.IN_PROGRESS;
    }
}
