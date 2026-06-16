using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Bookings.Commands.CreateBooking;

public sealed record CreateBookingResponse(
    long BookingId,
    BookingType BookingType,
    BookingStatus BookingStatus,
    DateTime? ScheduledAt,
    double EstimatedDistanceKm,
    int EstimatedDurationMinutes,
    decimal EstimatedFare,
    string? EncodedPolyline,
    string Message);
