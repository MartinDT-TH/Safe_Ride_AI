using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.Bookings.Commands.CreateBooking;
using SafeRide.Application.Features.Bookings.DTOs;
using SafeRide.Application.Features.Drivers.Services;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using System.Data;
using System.Text.Json;

namespace SafeRide.Infrastructure.Services;

public sealed class BookingAssignmentService : IBookingAssignmentService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILicenseCompatibilityService _licenseCompatibilityService;
    private readonly IVehicleLicenseRequirementService _vehicleLicenseRequirementService;
    private readonly IRedisService _redisService;
    private readonly IRealtimeNotificationService _realtimeNotificationService;
    private readonly IBookingMatchingService _bookingMatchingService;
    private readonly IMatchingPolicyProvider _matchingPolicyProvider;
    private readonly IBookingLifecycleJobScheduler _jobScheduler;
    private readonly IOptionsMonitor<TripTrackingOptions> _tripTrackingOptions;
    private readonly DriverCompensationOptions _compensationOptions;

    public BookingAssignmentService(
        ApplicationDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ILicenseCompatibilityService licenseCompatibilityService,
        IVehicleLicenseRequirementService vehicleLicenseRequirementService,
        IRedisService redisService,
        IRealtimeNotificationService realtimeNotificationService,
        IBookingMatchingService bookingMatchingService,
        IMatchingPolicyProvider matchingPolicyProvider,
        IBookingLifecycleJobScheduler jobScheduler,
        IOptionsMonitor<TripTrackingOptions> tripTrackingOptions,
        IOptions<DriverCompensationOptions> compensationOptions)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _licenseCompatibilityService = licenseCompatibilityService;
        _vehicleLicenseRequirementService = vehicleLicenseRequirementService;
        _redisService = redisService;
        _realtimeNotificationService = realtimeNotificationService;
        _bookingMatchingService = bookingMatchingService;
        _matchingPolicyProvider = matchingPolicyProvider;
        _jobScheduler = jobScheduler;
        _tripTrackingOptions = tripTrackingOptions;
        _compensationOptions = compensationOptions.Value;
    }

    public Task<CreateBookingResponse> ConfirmDriverAsync(
        Guid customerId,
        long bookingId,
        long? offerId,
        CancellationToken cancellationToken) =>
        _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => ConfirmDriverCoreAsync(
                customerId,
                bookingId,
                offerId,
                cancellationToken));

    private async Task<CreateBookingResponse> ConfirmDriverCoreAsync(
        Guid customerId,
        long bookingId,
        long? offerId,
        CancellationToken cancellationToken)
    {
        var utcNow = _dateTimeProvider.UtcNow;

        // Flow: serialize customer confirmation so one booking/driver cannot produce duplicate active trips.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var booking = await _dbContext.Bookings
            .Include(x => x.Vehicle)
            .Include(x => x.BookingPromotions)
                .ThenInclude(x => x.Promotion)
            .FirstOrDefaultAsync(
                x => x.BookingId == bookingId && x.CustomerId == customerId,
                cancellationToken)
            ?? throw new BookingException(
                "booking.not_found",
                "Không tìm thấy chuyến của bạn.",
                404);

        var offer = await _dbContext.BookingDriverOffers
            .Where(x => x.BookingId == bookingId
                && (!offerId.HasValue || x.Id == offerId.Value)
                && (x.OfferStatus == DriverOfferStatus.DriverAccepted
                    || x.OfferStatus == DriverOfferStatus.CustomerConfirmed))
            .OrderByDescending(x => x.ConfirmedAt ?? x.OfferedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BookingException(
                "booking.driver_offer_not_found",
                "Chưa có tài xế phù hợp để xác nhận.",
                409);

        // Flow: idempotent retry returns the existing confirmed trip instead of creating another one.
        var trip = await _dbContext.Trips
            .FirstOrDefaultAsync(x => x.BookingId == bookingId, cancellationToken);

        if (offer.OfferStatus == DriverOfferStatus.CustomerConfirmed
            && booking.BookingStatus == BookingStatus.DriverAssigned
            && trip is not null
            && trip.DriverId == offer.DriverId)
        {
            await transaction.CommitAsync(cancellationToken);
            var confirmedOfferDto = await GetOfferDtoAsync(offer.Id, utcNow, cancellationToken);
            return BuildResponse(
                booking,
                booking.BookingType == BookingType.Scheduled
                    ? "Chuyến đặt trước đã có tài xế. Tài xế đang chuẩn bị đến điểm đón."
                    : "Đã xác nhận thuê tài xế. Tài xế đang di chuyển đến điểm đón.",
                trip.Id,
                confirmedOfferDto,
                trip.TripStatus);
        }

        if (booking.BookingType == BookingType.Scheduled)
        {
            throw new BookingException(
                "booking.scheduled_driver_auto_confirmed",
                "Chuyến đặt trước được hệ thống tự động xác nhận khi tài xế nhận chuyến.",
                409);
        }

        if (booking.BookingStatus != BookingStatus.Searching)
        {
            throw new BookingException(
                "booking.cannot_confirm_driver",
                "Chuyến này không còn ở trạng thái tìm tài xế.",
                409);
        }

        if (offer.OfferStatus != DriverOfferStatus.DriverAccepted)
        {
            throw new BookingException(
                "booking.driver_offer_not_ready",
                "Tài xế chưa sẵn sàng để bạn xác nhận thuê.",
                409);
        }

        if (offer.ExpiresAt <= utcNow)
        {
            offer.OfferStatus = DriverOfferStatus.Expired;
            offer.ExpiredAt = utcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await _jobScheduler.CancelExpireDriverOfferAsync(offer.Id, cancellationToken);
            await RemoveOfferKeysAsync(offer.BookingId, offer.DriverId);

            throw new BookingException(
                "booking.driver_offer_expired",
                "Tài xế không còn khả dụng. SafeRide đang tìm tài xế khác cho bạn.",
                409);
        }

        // Flow: re-check driver availability, license compatibility, and active trip guards inside the transaction.
        var driverProfile = await _dbContext.DriverProfiles
            .FirstOrDefaultAsync(x => x.DriverId == offer.DriverId, cancellationToken)
            ?? throw new BookingException(
                "booking.driver_unavailable",
                "Tài xế không còn sẵn sàng. SafeRide đang tìm tài xế khác cho bạn.",
                409);

        if (driverProfile.WorkStatus != DriverWorkStatus.Online)
        {
            throw new BookingException(
                "booking.driver_unavailable",
                "Tài xế không còn sẵn sàng. SafeRide đang tìm tài xế khác cho bạn.",
                409);
        }

        await EnsureDriverCanServeBookingAsync(driverProfile, booking, offer, cancellationToken);
        await EnsureDriverHasNoActiveTripAsync(offer.DriverId, cancellationToken);

        var assignment = await CompleteDriverAssignmentAsync(
            booking,
            offer,
            driverProfile,
            DriverOfferStatus.DriverAccepted,
            utcNow,
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var driverOffer = await RunAssignmentPostCommitAsync(
            booking,
            offer,
            assignment,
            isSystemAutoConfirmed: false,
            utcNow,
            cancellationToken);

        return BuildResponse(
            booking,
            "Đã xác nhận thuê tài xế. Tài xế đang di chuyển đến điểm đón.",
            assignment.Trip.Id,
            driverOffer,
            assignment.Trip.TripStatus);
    }

    public Task<CreateBookingResponse> RejectDriverAsync(
        Guid customerId,
        long bookingId,
        CancellationToken cancellationToken) =>
        _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => RejectDriverCoreAsync(customerId, bookingId, cancellationToken));

    private async Task<CreateBookingResponse> RejectDriverCoreAsync(
        Guid customerId,
        long bookingId,
        CancellationToken cancellationToken)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Flow: customer rejection closes the accepted offer, clears matching keys, then starts another match attempt.
        var booking = await _dbContext.Bookings
            .Include(x => x.BookingPromotions)
                .ThenInclude(x => x.Promotion)
            .FirstOrDefaultAsync(
                x => x.BookingId == bookingId && x.CustomerId == customerId,
                cancellationToken)
            ?? throw new BookingException(
                "booking.not_found",
                "Không tìm thấy chuyến của bạn.",
                404);

        if (booking.BookingType == BookingType.Scheduled)
        {
            throw new BookingException(
                "booking.scheduled_driver_auto_confirmed",
                "Chuyến đặt trước không yêu cầu khách hàng chọn lại tài xế.",
                409);
        }

        if (booking.BookingStatus != BookingStatus.Searching)
        {
            throw new BookingException(
                "booking.cannot_reject_driver",
                "Chuyến này không còn ở trạng thái tìm tài xế.",
                409);
        }

        var offer = await _dbContext.BookingDriverOffers
            .Where(x => x.BookingId == bookingId
                && x.OfferStatus == DriverOfferStatus.DriverAccepted)
            .OrderByDescending(x => x.ConfirmedAt ?? x.OfferedAt)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BookingException(
                "booking.driver_offer_not_found",
                "Chưa có tài xế để từ chối.",
                409);

        offer.OfferStatus = DriverOfferStatus.Rejected;
        offer.CancelledAt = utcNow;
        booking.UpdatedAt = utcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await _jobScheduler.CancelExpireDriverOfferAsync(offer.Id, cancellationToken);
        await RemoveOfferKeysAsync(offer.BookingId, offer.DriverId);

        await _realtimeNotificationService.PublishDriverOfferRejectedAsync(
            new DriverOfferRejectedEvent(
                booking.BookingId,
                booking.CustomerId,
                offer.DriverId,
                offer.Id,
                utcNow),
            cancellationToken);

        // Driver clients treat cancellation/expiry events as the signal to close
        // the customer-confirmation waiting state.
        await _realtimeNotificationService.PublishDriverOfferCancelledAsync(
            new DriverOfferCancelledEvent(
                booking.BookingId,
                booking.CustomerId,
                offer.DriverId,
                offer.Id,
                utcNow,
                "Khách hàng đã từ chối yêu cầu nhận chuyến."),
            cancellationToken);

        var nextOffer = await _bookingMatchingService.StartMatchingAsync(
            booking.BookingId,
            cancellationToken);

        return BuildResponse(
            booking,
            nextOffer is null
                ? "SafeRide đang tiếp tục tìm tài xế phù hợp cho bạn."
                : "SafeRide đang tiếp tục tìm tài xế phù hợp cho bạn.",
            null,
            nextOffer);
    }

    public Task<CreateBookingResponse> AcceptDriverOfferAsync(
        Guid driverId,
        long offerId,
        CancellationToken cancellationToken) =>
        _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            () => AcceptDriverOfferCoreAsync(driverId, offerId, cancellationToken));

    private async Task<CreateBookingResponse> AcceptDriverOfferCoreAsync(
        Guid driverId,
        long offerId,
        CancellationToken cancellationToken)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        BookingDriverOffer offer;
        Booking booking;

        // Flow: Now bookings wait for customer confirmation; scheduled bookings are assigned atomically here.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        offer = await _dbContext.BookingDriverOffers
            .Include(x => x.Booking)
                .ThenInclude(x => x.Vehicle)
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .FirstOrDefaultAsync(
                x => x.Id == offerId && x.DriverId == driverId,
                cancellationToken)
            ?? throw new BookingException(
                "driver_offer.not_found",
                "Không tìm thấy yêu cầu nhận chuyến.",
                404);

        booking = offer.Booking;

        var existingTrip = await _dbContext.Trips
            .FirstOrDefaultAsync(x => x.BookingId == booking.BookingId, cancellationToken);
        if (booking.BookingType == BookingType.Now
            && booking.BookingStatus == BookingStatus.Searching
            && offer.OfferStatus == DriverOfferStatus.DriverAccepted
            && offer.ExpiresAt > utcNow
            && existingTrip is null)
        {
            await transaction.CommitAsync(cancellationToken);
            var acceptedOffer = await RunNowAcceptancePostCommitAsync(
                booking,
                offer,
                utcNow,
                cancellationToken);
            return BuildResponse(
                booking,
                "Tài xế phù hợp đã sẵn sàng.",
                null,
                acceptedOffer);
        }

        if (booking.BookingType == BookingType.Scheduled
            && booking.BookingStatus == BookingStatus.DriverAssigned
            && offer.OfferStatus == DriverOfferStatus.CustomerConfirmed
            && existingTrip is not null
            && existingTrip.DriverId == driverId)
        {
            var previouslyCancelledOffers = await GetOffersCancelledByAssignmentAsync(
                booking.BookingId,
                offer,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var existingAssignment = new DriverAssignmentResult(
                existingTrip,
                previouslyCancelledOffers);
            var existingOfferDto = await RunAssignmentPostCommitAsync(
                booking,
                offer,
                existingAssignment,
                isSystemAutoConfirmed: true,
                utcNow,
                cancellationToken);
            return BuildResponse(
                booking,
                "Chuyến đặt trước đã có tài xế. Tài xế đang chuẩn bị đến điểm đón.",
                existingTrip.Id,
                existingOfferDto,
                existingTrip.TripStatus);
        }

        if (booking.BookingStatus != BookingStatus.Searching)
        {
            throw new BookingException(
                "driver_offer.booking_not_searching",
                "Chuyến này không còn ở trạng thái tìm tài xế.",
                409);
        }

        if (offer.OfferStatus != DriverOfferStatus.Sent)
        {
            throw new BookingException(
                "driver_offer.not_active",
                "Yêu cầu nhận chuyến không còn hiệu lực.",
                409);
        }

        if (offer.ExpiresAt <= utcNow)
        {
            offer.OfferStatus = DriverOfferStatus.Expired;
            offer.ExpiredAt = utcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await _jobScheduler.CancelExpireDriverOfferAsync(offer.Id, cancellationToken);
            await RemoveOfferKeysAsync(offer.BookingId, offer.DriverId);

            await _realtimeNotificationService.PublishDriverOfferExpiredAsync(
                new DriverOfferExpiredEvent(
                    offer.BookingId,
                    booking.CustomerId,
                    driverId,
                    offer.Id,
                    utcNow,
                    "Yêu cầu nhận chuyến đã hết hạn."),
                cancellationToken);

            throw new BookingException(
                "driver_offer.expired",
                "Yêu cầu nhận chuyến đã hết hạn.",
                409);
        }

        var driverProfile = await EnsureDriverOnlineAsync(driverId, cancellationToken);
        await EnsureDriverCanServeBookingAsync(driverProfile, booking, offer, cancellationToken);
        await EnsureDriverHasNoActiveTripAsync(driverId, cancellationToken);
        await EnsureNoOtherActiveDriverOfferAsync(driverId, offer.Id, cancellationToken);

        if (booking.BookingType == BookingType.Scheduled)
        {
            await EnsureNoWinningBookingOfferAsync(
                booking.BookingId,
                offer.Id,
                cancellationToken);

            var assignment = await CompleteDriverAssignmentAsync(
                booking,
                offer,
                driverProfile,
                DriverOfferStatus.Sent,
                utcNow,
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var scheduledOffer = await RunAssignmentPostCommitAsync(
                booking,
                offer,
                assignment,
                isSystemAutoConfirmed: true,
                utcNow,
                cancellationToken);
            return BuildResponse(
                booking,
                "Chuyến đặt trước đã có tài xế. Tài xế đang chuẩn bị đến điểm đón.",
                assignment.Trip.Id,
                scheduledOffer,
                assignment.Trip.TripStatus);
        }

        await EnsureNoOtherActiveBookingOfferAsync(booking.BookingId, offer.Id, cancellationToken);

        // Flow: extend the offer into the customer-confirm window and refresh the matching lock/cache.
        offer.OfferStatus = DriverOfferStatus.DriverAccepted;
        offer.ConfirmedAt = utcNow;
        offer.ExpiresAt = utcNow.AddSeconds(_matchingPolicyProvider.Current.CustomerConfirmExpireSeconds);
        driverProfile.LastActiveAt = utcNow;
        driverProfile.UpdatedAt = utcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _jobScheduler.CancelExpireDriverOfferAsync(offer.Id, cancellationToken);
        _jobScheduler.ScheduleExpireDriverOffer(
            offer.Id,
            offer.ExpiresAt - utcNow);
        await CacheMatchingOfferAsync(offer);
        await _redisService.SetAsync(
            RedisKeys.MatchingDriverLock(driverId),
            booking.BookingId.ToString(),
            offer.ExpiresAt - utcNow);

        var driverOffer = await GetOfferDtoAsync(offer.Id, utcNow, cancellationToken);
        if (driverOffer is not null)
        {
            await _realtimeNotificationService.PublishDriverOfferAcceptedAsync(
                new DriverOfferAcceptedEvent(
                    booking.BookingId,
                    booking.CustomerId,
                    driverId,
                    offer.Id,
                    utcNow,
                    offer.ExpiresAt,
                    driverOffer,
                    "Tài xế phù hợp đã sẵn sàng."),
                cancellationToken);
        }

        return BuildResponse(
            booking,
            "Tài xế phù hợp đã sẵn sàng.",
            null,
            driverOffer);
    }

    private async Task<BookingDriverOfferDto?> RunNowAcceptancePostCommitAsync(
        Booking booking,
        BookingDriverOffer offer,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await _jobScheduler.CancelExpireDriverOfferAsync(offer.Id, cancellationToken);
        _jobScheduler.ScheduleExpireDriverOffer(
            offer.Id,
            offer.ExpiresAt - utcNow);
        await CacheMatchingOfferAsync(offer);
        await _redisService.SetAsync(
            RedisKeys.MatchingDriverLock(offer.DriverId),
            booking.BookingId.ToString(),
            offer.ExpiresAt - utcNow);

        var driverOffer = await GetOfferDtoAsync(offer.Id, utcNow, cancellationToken);
        if (driverOffer is not null)
        {
            await _realtimeNotificationService.PublishDriverOfferAcceptedAsync(
                new DriverOfferAcceptedEvent(
                    booking.BookingId,
                    booking.CustomerId,
                    offer.DriverId,
                    offer.Id,
                    utcNow,
                    offer.ExpiresAt,
                    driverOffer,
                    "Tài xế phù hợp đã sẵn sàng."),
                cancellationToken);
        }

        return driverOffer;
    }

    public async Task RejectDriverOfferAsync(
        Guid driverId,
        long offerId,
        CancellationToken cancellationToken)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        // Flow: driver rejection is terminal for this offer and immediately triggers another match attempt.
        var offer = await _dbContext.BookingDriverOffers
            .Include(x => x.Booking)
            .FirstOrDefaultAsync(
                x => x.Id == offerId && x.DriverId == driverId,
                cancellationToken)
            ?? throw new BookingException(
                "driver_offer.not_found",
                "Không tìm thấy yêu cầu nhận chuyến.",
                404);

        if (offer.OfferStatus != DriverOfferStatus.Sent)
        {
            return;
        }

        offer.OfferStatus = DriverOfferStatus.Rejected;
        offer.CancelledAt = utcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _jobScheduler.CancelExpireDriverOfferAsync(offer.Id, cancellationToken);
        await RemoveOfferKeysAsync(offer.BookingId, offer.DriverId);

        await _realtimeNotificationService.PublishDriverOfferRejectedAsync(
            new DriverOfferRejectedEvent(
                offer.BookingId,
                offer.Booking.CustomerId,
                driverId,
                offer.Id,
                utcNow),
            cancellationToken);

        await _bookingMatchingService.StartMatchingAsync(
            offer.BookingId,
            cancellationToken);
    }

    private async Task<DriverProfile> EnsureDriverOnlineAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var redisOnline = await _redisService.GetAsync(RedisKeys.DriverOnline(driverId));
        var redisStatus = await _redisService.GetAsync(RedisKeys.DriverStatus(driverId));
        var driverProfile = await _dbContext.DriverProfiles
            .FirstOrDefaultAsync(x => x.DriverId == driverId, cancellationToken);

        if (driverProfile is null
            || driverProfile.WorkStatus != DriverWorkStatus.Online
            || redisOnline is null
            || !string.Equals(redisStatus, DriverWorkStatus.Online.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new BookingException(
                "driver_offer.driver_unavailable",
                "Bạn không còn ở trạng thái sẵn sàng nhận chuyến.",
                409);
        }

        return driverProfile;
    }

    private async Task EnsureDriverCanServeBookingAsync(
        DriverProfile driverProfile,
        Booking booking,
        BookingDriverOffer offer,
        CancellationToken cancellationToken)
    {
        if (!_vehicleLicenseRequirementService.HasValidRequirement(booking.Vehicle))
        {
            throw new BookingException(
                "booking.invalid_vehicle_license_requirement",
                "Không xác định được hạng bằng lái cần thiết cho xe đã chọn.",
                400);
        }

        var driverLicenses = await _dbContext.DriverKycs
            .AsNoTracking()
            .Where(x => x.DriverId == driverProfile.DriverId
                && x.DocumentType == KycDocumentType.DRIVING_LICENSE
                && x.KycStatus == KycStatus.Approved
                && x.LicenseClass.HasValue)
            .Select(x => x.LicenseClass)
            .ToListAsync(cancellationToken);

        var canServeBooking = driverLicenses.Any(driverLicense =>
            driverLicense.HasValue
            && _licenseCompatibilityService.CanDrive(
                driverLicense.Value,
                booking.Vehicle.RequiredLicenseClass));

        if (!canServeBooking)
        {
            throw new BookingException(
                "driver_offer.license_not_compatible",
                "Bằng lái của tài xế không phù hợp với chuyến này.",
                409);
        }

        if (DriverCompensationEligibility.ExceedsMaximumTripDistance(
                booking,
                _compensationOptions))
        {
            throw new BookingException(
                "driver_offer.trip_distance_exceeds_maximum",
                "Khoảng cách chuyến đi vượt quá giới hạn nhận chuyến.",
                409);
        }

        if (DriverCompensationEligibility.RequiresLongDistanceOptIn(
                booking,
                _compensationOptions)
            && !driverProfile.AcceptLongDistanceTrips)
        {
            throw new BookingException(
                "driver_offer.long_distance_opt_in_required",
                "Tài xế chưa bật nhận chuyến đường dài.",
                409);
        }

        // Never recalculate pickup routing at acceptance/assignment. Legacy offers without
        // a Phase 3 snapshot remain accept-able for backwards compatibility.
        if (offer.PickupDistanceKm.HasValue
            && DriverCompensationEligibility.RequiresLongPickupOptIn(
                offer.PickupDistanceKm.Value,
                _compensationOptions)
            && !driverProfile.AcceptLongPickupTrips)
        {
            throw new BookingException(
                "driver_offer.long_pickup_opt_in_required",
                "Tài xế chưa bật nhận chuyến có quãng đường đón xa.",
                409);
        }
    }

    private async Task EnsureDriverHasNoActiveTripAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var hasActiveTrip = await _dbContext.Trips
            .AnyAsync(
                x => x.DriverId == driverId
                    && x.TripStatus != TripStatus.COMPLETED
                    && x.TripStatus != TripStatus.CANCELLED,
                cancellationToken);
        if (hasActiveTrip)
        {
            throw new BookingException(
                "driver_offer.driver_busy",
                "Tài xế đang có chuyến khác chưa hoàn tất.",
                409);
        }
    }

    private async Task EnsureNoOtherActiveBookingOfferAsync(
        long bookingId,
        long currentOfferId,
        CancellationToken cancellationToken)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        var hasOtherActiveOffer = await _dbContext.BookingDriverOffers
            .AnyAsync(
                x => x.BookingId == bookingId
                    && x.Id != currentOfferId
                    && (x.OfferStatus == DriverOfferStatus.Sent
                        || x.OfferStatus == DriverOfferStatus.DriverAccepted)
                    && x.ExpiresAt > utcNow,
                cancellationToken);
        if (hasOtherActiveOffer)
        {
            throw new BookingException(
                "driver_offer.booking_already_has_driver",
                "Chuyến này đang có yêu cầu nhận chuyến khác.",
                409);
        }
    }

    private async Task EnsureNoWinningBookingOfferAsync(
        long bookingId,
        long currentOfferId,
        CancellationToken cancellationToken)
    {
        var hasWinningOffer = await _dbContext.BookingDriverOffers
            .AnyAsync(
                x => x.BookingId == bookingId
                    && x.Id != currentOfferId
                    && x.OfferStatus == DriverOfferStatus.CustomerConfirmed,
                cancellationToken);
        if (hasWinningOffer)
        {
            throw new BookingException(
                "driver_offer.booking_already_has_driver",
                "Chuyến này đã có tài xế nhận chuyến.",
                409);
        }
    }

    private async Task EnsureNoOtherActiveDriverOfferAsync(
        Guid driverId,
        long currentOfferId,
        CancellationToken cancellationToken)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        var hasOtherActiveOffer = await _dbContext.BookingDriverOffers
            .AnyAsync(
                x => x.DriverId == driverId
                    && x.Id != currentOfferId
                    && (x.OfferStatus == DriverOfferStatus.Sent
                        || x.OfferStatus == DriverOfferStatus.DriverAccepted)
                    && x.ExpiresAt > utcNow,
                cancellationToken);
        if (hasOtherActiveOffer)
        {
            throw new BookingException(
                "driver_offer.driver_has_active_offer",
                "Bạn đang có yêu cầu nhận chuyến khác chưa xử lý.",
                409);
        }
    }

    private async Task CacheTripLiveAsync(
        Trip trip,
        Guid customerId)
    {
        var assignedAt = trip.DriverAssignedAt ?? _dateTimeProvider.UtcNow;
        var cache = new TripLiveCache(
            trip.Id,
            trip.BookingId,
            trip.DriverId,
            customerId,
            trip.TripStatus,
            assignedAt);
        var driverActiveTrip = new DriverActiveTripCache(
            trip.Id,
            trip.BookingId,
            trip.DriverId,
            customerId,
            trip.TripStatus,
            assignedAt,
            trip.Booking.RoutePolyline,
            trip.Booking.DestinationLocation?.Y,
            trip.Booking.DestinationLocation?.X);

        await _redisService.SetAsync(
            RedisKeys.TripLive(trip.Id),
            JsonSerializer.Serialize(cache),
            TimeSpan.FromHours(_tripTrackingOptions.CurrentValue.TripLiveTtlHours));
        await _redisService.SetAsync(
            RedisKeys.DriverActiveTrip(trip.DriverId),
            JsonSerializer.Serialize(driverActiveTrip),
            TimeSpan.FromHours(_tripTrackingOptions.CurrentValue.TripLiveTtlHours));
    }

    private async Task<DriverAssignmentResult> CompleteDriverAssignmentAsync(
        Booking booking,
        BookingDriverOffer offer,
        DriverProfile driverProfile,
        DriverOfferStatus requiredOfferStatus,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (booking.BookingStatus != BookingStatus.Searching)
        {
            throw new BookingException(
                "driver_offer.booking_not_searching",
                "Chuyến này không còn ở trạng thái tìm tài xế.",
                409);
        }

        if (offer.OfferStatus != requiredOfferStatus || offer.ExpiresAt <= utcNow)
        {
            throw new BookingException(
                "driver_offer.not_active",
                "Yêu cầu nhận chuyến không còn hiệu lực.",
                409);
        }

        if (driverProfile.WorkStatus != DriverWorkStatus.Online)
        {
            throw new BookingException(
                "driver_offer.driver_unavailable",
                "Tài xế không còn sẵn sàng nhận chuyến.",
                409);
        }

        var existingTrip = await _dbContext.Trips
            .FirstOrDefaultAsync(x => x.BookingId == booking.BookingId, cancellationToken);
        if (existingTrip is not null)
        {
            throw new BookingException(
                "booking.trip_already_created",
                "Chuyến này đã có tài xế được xác nhận.",
                409);
        }

        await EnsureNoWinningBookingOfferAsync(booking.BookingId, offer.Id, cancellationToken);

        var trip = new Trip
        {
            BookingId = booking.BookingId,
            DriverId = offer.DriverId,
            TripStatus = TripStatus.ACCEPTED,
            DriverAssignedAt = utcNow,
            CreatedAt = utcNow
        };

        // CustomerConfirmed is reused for scheduled bookings as a system auto-confirmed
        // assignment. It does not represent a direct customer confirmation in that flow.
        offer.OfferStatus = DriverOfferStatus.CustomerConfirmed;
        offer.ConfirmedAt = utcNow;
        booking.BookingStatus = BookingStatus.DriverAssigned;
        booking.UpdatedAt = utcNow;
        driverProfile.WorkStatus = DriverWorkStatus.Busy;
        driverProfile.UpdatedAt = utcNow;

        var cancelledOffers = await _dbContext.BookingDriverOffers
            .Where(x => x.BookingId == booking.BookingId
                && x.Id != offer.Id
                && (x.OfferStatus == DriverOfferStatus.Sent
                    || x.OfferStatus == DriverOfferStatus.DriverAccepted))
            .ToListAsync(cancellationToken);
        foreach (var cancelledOffer in cancelledOffers)
        {
            cancelledOffer.OfferStatus = DriverOfferStatus.Cancelled;
            cancelledOffer.CancelledAt = utcNow;
        }

        await _dbContext.Trips.AddAsync(trip, cancellationToken);
        return new DriverAssignmentResult(trip, cancelledOffers);
    }

    private Task<List<BookingDriverOffer>> GetOffersCancelledByAssignmentAsync(
        long bookingId,
        BookingDriverOffer winningOffer,
        CancellationToken cancellationToken)
    {
        return _dbContext.BookingDriverOffers
            .Where(x => x.BookingId == bookingId
                && x.Id != winningOffer.Id
                && x.OfferStatus == DriverOfferStatus.Cancelled
                && x.CancelledAt == winningOffer.ConfirmedAt)
            .ToListAsync(cancellationToken);
    }

    private async Task<BookingDriverOfferDto?> RunAssignmentPostCommitAsync(
        Booking booking,
        BookingDriverOffer offer,
        DriverAssignmentResult assignment,
        bool isSystemAutoConfirmed,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        await _jobScheduler.CancelJobsForBookingAsync(booking.BookingId, cancellationToken);
        await _jobScheduler.CancelExpireDriverOfferAsync(offer.Id, cancellationToken);

        await CacheTripLiveAsync(assignment.Trip, booking.CustomerId);
        await RemoveMatchingKeysAsync(booking.BookingId, offer.DriverId);
        await _redisService.SetAsync(
            RedisKeys.DriverStatus(offer.DriverId),
            DriverWorkStatus.Busy.ToString(),
            TimeSpan.FromMinutes(_tripTrackingOptions.CurrentValue.DriverStatusTtlMinutes));

        foreach (var cancelledOffer in assignment.CancelledOffers)
        {
            await _jobScheduler.CancelExpireDriverOfferAsync(cancelledOffer.Id, cancellationToken);
            await RemoveOfferKeysAsync(cancelledOffer.BookingId, cancelledOffer.DriverId);
        }

        var driverOffer = await GetOfferDtoAsync(offer.Id, utcNow, cancellationToken);
        var assignmentMessage = isSystemAutoConfirmed
            ? "Tài xế đã nhận chuyến đặt trước. Hệ thống đã tự động xác nhận tài xế."
            : "Đã xác nhận thuê tài xế. Tài xế đang di chuyển đến điểm đón.";

        if (!isSystemAutoConfirmed)
        {
            await _realtimeNotificationService.PublishCustomerConfirmedDriverOfferAsync(
                new CustomerConfirmedDriverOfferEvent(
                    booking.BookingId,
                    assignment.Trip.Id,
                    booking.CustomerId,
                    offer.DriverId,
                    offer.Id,
                    utcNow,
                    "Khách hàng đã xác nhận thuê tài xế."),
                cancellationToken);
        }

        await _realtimeNotificationService.PublishBookingStatusChangedAsync(
            new BookingStatusChangedEvent(
                booking.BookingId,
                booking.CustomerId,
                booking.BookingStatus,
                utcNow),
            cancellationToken);
        await _realtimeNotificationService.PublishBookingDriverAssignedAsync(
            new BookingDriverAssignedEvent(
                booking.BookingId,
                assignment.Trip.Id,
                booking.CustomerId,
                offer.DriverId,
                utcNow,
                assignmentMessage,
                driverOffer),
            cancellationToken);
        await _realtimeNotificationService.PublishTripCreatedAsync(
            new TripCreatedEvent(
                assignment.Trip.Id,
                booking.BookingId,
                booking.CustomerId,
                assignment.Trip.DriverId,
                assignment.Trip.TripStatus,
                assignment.Trip.DriverAssignedAt ?? utcNow),
            cancellationToken);
        await _realtimeNotificationService.PublishTripStatusChangedAsync(
            new TripStatusChangedEvent(
                assignment.Trip.Id,
                booking.BookingId,
                booking.CustomerId,
                assignment.Trip.DriverId,
                assignment.Trip.TripStatus,
                utcNow),
            cancellationToken);

        foreach (var cancelledOffer in assignment.CancelledOffers)
        {
            await _realtimeNotificationService.PublishDriverOfferCancelledAsync(
                new DriverOfferCancelledEvent(
                    cancelledOffer.BookingId,
                    booking.CustomerId,
                    cancelledOffer.DriverId,
                    cancelledOffer.Id,
                    utcNow,
                    "Yêu cầu nhận chuyến đã được hủy vì chuyến đã có tài xế khác."),
                cancellationToken);
        }

        return driverOffer;
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
            offer.ExpiresAt - _dateTimeProvider.UtcNow);
    }

    private async Task RemoveMatchingKeysAsync(
        long bookingId,
        Guid driverId)
    {
        await _redisService.RemoveAsync(RedisKeys.MatchingBooking(bookingId));
        await RemoveOfferKeysAsync(bookingId, driverId);
    }

    private async Task RemoveOfferKeysAsync(
        long bookingId,
        Guid driverId)
    {
        await _redisService.RemoveAsync(RedisKeys.MatchingOffer(bookingId, driverId));
        await _redisService.RemoveAsync(RedisKeys.MatchingDriverLock(driverId));
    }

    private async Task<BookingDriverOfferDto?> GetOfferDtoAsync(
        long offerId,
        DateTime utcNow,
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
                LicenseClass = kyc.LicenseClass ?? LicenseClass.A1,
                driverOffer.ExpiresAt,
                driverOffer.OfferStatus
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
                AverageRating = group.Average(x => (double)x.RatingScore),
                TripCount = group.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        var customerConfirmRemainingSeconds =
            confirmedOffer.OfferStatus == DriverOfferStatus.DriverAccepted
                ? (int?)Math.Max(0, (int)Math.Ceiling((confirmedOffer.ExpiresAt - utcNow).TotalSeconds))
                : null;

        double? driverLatitude = null;
        double? driverLongitude = null;
        var locationJson = await _redisService.GetAsync(RedisKeys.DriverLocation(confirmedOffer.DriverId));
        if (!string.IsNullOrEmpty(locationJson))
        {
            var cache = JsonSerializer.Deserialize<DriverLocationCache>(locationJson);
            if (cache is not null)
            {
                driverLatitude = cache.Latitude;
                driverLongitude = cache.Longitude;
            }
        }

        return new BookingDriverOfferDto(
            confirmedOffer.Id,
            confirmedOffer.DriverId,
            confirmedOffer.FullName ?? confirmedOffer.UserName ?? "Tài xế SafeRide",
            confirmedOffer.AvatarUrl,
            ratingStats is null ? 0 : Math.Round(ratingStats.AverageRating, 1),
            ratingStats?.TripCount ?? 0,
            confirmedOffer.ExperienceYears ?? 0,
            confirmedOffer.LicenseClass,
            confirmedOffer.ExpiresAt,
            confirmedOffer.OfferStatus,
            driverLatitude,
            driverLongitude,
            customerConfirmRemainingSeconds);
    }

    private static CreateBookingResponse BuildResponse(
        Booking booking,
        string message,
        long? tripId,
        BookingDriverOfferDto? driverOffer,
        TripStatus? tripStatus = null)
    {
        var price = BookingPriceMapper.FromBooking(booking);

        return new CreateBookingResponse(
            booking.BookingId,
            booking.BookingType,
            booking.BookingStatus,
            booking.ScheduledAt,
            (double)(booking.EstimatedDistanceKm ?? 0m),
            booking.EstimatedDurationMinutes ?? 0,
            booking.EstimatedFare,
            price.OriginalFare,
            price.PromotionCode,
            price.DiscountAmount,
            price.FinalFare,
            booking.RoutePolyline,
            message,
            tripId,
            driverOffer,
            tripStatus);
    }

    private sealed record DriverAssignmentResult(
        Trip Trip,
        List<BookingDriverOffer> CancelledOffers);

}
