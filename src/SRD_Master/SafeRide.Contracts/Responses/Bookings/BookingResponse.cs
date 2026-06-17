using SafeRide.Domain.Enums;

namespace SafeRide.Contracts.Responses.Bookings;

public sealed record BookingResponse(
    long BookingId,
    BookingType BookingType,
    BookingStatus BookingStatus,
    DateTime? ScheduledAt,
    double EstimatedDistanceKm,
    int EstimatedDurationMinutes,
    decimal EstimatedFare,
    string? EncodedPolyline,
    string Message);
