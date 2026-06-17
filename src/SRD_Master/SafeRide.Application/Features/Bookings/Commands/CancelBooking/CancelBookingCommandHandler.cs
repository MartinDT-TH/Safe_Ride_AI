using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Bookings.Commands.CancelBooking;

public sealed class CancelBookingCommandHandler
    : IRequestHandler<CancelBookingCommand, CancelBookingResponse>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CancelBookingCommandHandler(
        IBookingRepository bookingRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _bookingRepository = bookingRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<CancelBookingResponse> Handle(
        CancelBookingCommand request,
        CancellationToken cancellationToken)
    {
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

        if (!CanCancel(booking.BookingStatus))
        {
            throw new BookingException(
                "booking.cannot_cancel",
                "Chuyến này đã có tài xế hoặc đã kết thúc, không thể hủy bằng thao tác quay lại.",
                409);
        }

        booking.BookingStatus = BookingStatus.Cancelled;
        booking.CancelledBy = request.CustomerId;
        booking.CancellationReason = NormalizeReason(request.Reason);
        booking.UpdatedAt = _dateTimeProvider.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResponse(booking, "Đã hủy chuyến thành công.");
    }

    private static bool CanCancel(BookingStatus status)
    {
        return status is BookingStatus.Searching or BookingStatus.PendingSchedule;
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
