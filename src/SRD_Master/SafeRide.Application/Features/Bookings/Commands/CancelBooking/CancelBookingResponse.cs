using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Bookings.Commands.CancelBooking;

public sealed record CancelBookingResponse(
    long BookingId,
    BookingType BookingType,
    BookingStatus BookingStatus,
    DateTime? ScheduledAt,
    double EstimatedDistanceKm,
    int EstimatedDurationMinutes,
    decimal EstimatedFare,
    string? EncodedPolyline,
    string Message);
