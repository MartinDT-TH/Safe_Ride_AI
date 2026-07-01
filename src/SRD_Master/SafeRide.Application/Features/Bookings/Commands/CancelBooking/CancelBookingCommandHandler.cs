using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Bookings.Commands.CancelBooking;

public sealed class CancelBookingCommandHandler
    : IRequestHandler<CancelBookingCommand, CancelBookingResponse>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IRealtimeNotificationService _realtimeNotificationService;
    private readonly IPromotionRepository _promotionRepository;
    private readonly IBookingLifecycleJobScheduler _jobScheduler;

    public CancelBookingCommandHandler(
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider,
        IRealtimeNotificationService realtimeNotificationService,
        IPromotionRepository promotionRepository,
        IBookingLifecycleJobScheduler jobScheduler)
    {
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
        _realtimeNotificationService = realtimeNotificationService;
        _promotionRepository = promotionRepository;
        _jobScheduler = jobScheduler;
    }

    public async Task<CancelBookingResponse> Handle(
        CancelBookingCommand request,
        CancellationToken cancellationToken)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        await _bookingRepository.ExpireStaleNowBookingsAsync(
            request.CustomerId,
            utcNow,
            cancellationToken);

        var booking = await _bookingRepository.GetCustomerBookingAsync(
            request.BookingId,
            request.CustomerId,
            cancellationToken);
        if (booking is null)
        {
            throw new BookingException(
                "booking.not_found",
                "Không tìm thấy chuyến của bạn.",
                404);
        }

        if (booking.BookingStatus == BookingStatus.Cancelled)
        {
            return ToResponse(booking, "Chuyến đã được hủy trước đó.");
        }

        if (booking.BookingStatus == BookingStatus.Expired)
        {
            return ToResponse(booking, "Chuyến đã hết thời gian chờ và được tự động kết thúc.");
        }

        if (!CanCancel(booking))
        {
            throw new BookingException(
                "booking.cannot_cancel",
                "Chỉ có thể hủy chuyến khi tài xế đang đến điểm đón.",
                409);
        }

        var previousStatus = booking.BookingStatus;
        var reason = NormalizeReason(request.Reason);
        if (previousStatus == BookingStatus.DriverAssigned)
        {
            var cancelledTrip = await _bookingRepository.CancelAssignedTripAsync(
                booking.BookingId,
                request.CustomerId,
                reason,
                utcNow,
                cancellationToken);
            if (!cancelledTrip)
            {
                throw new BookingException(
                    "booking.cannot_cancel_started_trip",
                    "Chuyến này đã bắt đầu hoặc đã kết thúc, không thể hủy.",
                    409);
            }
        }

        booking.BookingStatus = BookingStatus.Cancelled;
        booking.CancelledBy = request.CustomerId;
        booking.CancellationReason = reason;
        booking.UpdatedAt = utcNow;

        await _promotionRepository.RemoveBookingPromotionsForBookingAsync(
            booking.BookingId,
            cancellationToken);

        await _jobScheduler.CancelJobsForBookingAsync(booking.BookingId, cancellationToken);

        await _bookingRepository.CancelActiveDriverOffersAsync(
            booking.BookingId,
            utcNow,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _realtimeNotificationService.PublishBookingStatusChangedAsync(
            new BookingStatusChangedEvent(
                booking.BookingId,
                booking.CustomerId,
                booking.BookingStatus,
                utcNow),
            cancellationToken);

        if (booking.Trip?.TripStatus == TripStatus.CANCELLED)
        {
            await _realtimeNotificationService.PublishTripStatusChangedAsync(
                new TripStatusChangedEvent(
                    booking.Trip.Id,
                    booking.BookingId,
                    booking.CustomerId,
                    booking.Trip.DriverId,
                    booking.Trip.TripStatus,
                    utcNow),
                cancellationToken);
        }

        return ToResponse(booking, "Đã hủy chuyến thành công.");
    }

    private static bool CanCancel(Domain.Entities.Booking booking)
    {
        if (booking.BookingStatus == BookingStatus.Searching)
        {
            return true;
        }

        if (booking.BookingStatus == BookingStatus.DriverAssigned)
        {
            return booking.Trip?.TripStatus == TripStatus.ACCEPTED
                || booking.Trip?.TripStatus == TripStatus.DRIVER_ARRIVING
                || booking.Trip?.TripStatus == TripStatus.ARRIVED;
        }

        return false;
    }

    private static string? NormalizeReason(string? reason)
    {
        var normalized = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length > 255
            ? normalized[..255]
            : normalized;
    }

    private static CancelBookingResponse ToResponse(
        Domain.Entities.Booking booking,
        string message)
    {
        return new CancelBookingResponse(
            booking.BookingId,
            booking.BookingType,
            booking.BookingStatus,
            booking.ScheduledAt,
            (double)(booking.EstimatedDistanceKm ?? 0m),
            booking.EstimatedDurationMinutes ?? 0,
            booking.EstimatedFare,
            booking.RoutePolyline,
            message);
    }
}
