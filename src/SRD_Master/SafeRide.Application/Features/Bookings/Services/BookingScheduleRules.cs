using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Bookings.Services;

public static class BookingScheduleRules
{
    public static DateTime ResolveSurgeEvaluationTime(
        BookingType bookingType,
        DateTime? scheduledAt,
        DateTime utcNow)
    {
        if (!Enum.IsDefined(bookingType))
        {
            throw new BookingException(
                "booking.invalid_type",
                "Loại đặt chuyến không hợp lệ.",
                400);
        }

        if (bookingType == BookingType.Now && scheduledAt.HasValue)
        {
            throw new BookingException(
                "booking.schedule_not_allowed",
                "Chuyến đi ngay không được có thời gian đặt trước.",
                400);
        }

        if (bookingType == BookingType.Scheduled
            && (!scheduledAt.HasValue || scheduledAt.Value < utcNow.AddMinutes(30)))
        {
            throw new BookingException(
                "booking.invalid_schedule",
                "Thời gian đặt trước phải cách thời điểm hiện tại ít nhất 30 phút.",
                400);
        }

        if (scheduledAt.HasValue && scheduledAt.Value.Kind == DateTimeKind.Local)
        {
            throw new BookingException(
                "booking.schedule_must_be_utc",
                "Thời gian đặt trước phải được gửi theo múi giờ UTC.",
                400);
        }

        return bookingType == BookingType.Scheduled
            ? scheduledAt!.Value
            : utcNow;
    }
}
