using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Bookings.Commands.CreateBooking;

public sealed record CreateBookingResponse(
    long BookingId,
    BookingType BookingType,
    BookingStatus BookingStatus,
    DateTime? ScheduledAt,
    decimal EstimatedFare,
    string Message);
