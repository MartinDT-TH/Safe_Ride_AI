using SafeRide.Domain.Enums;

namespace SafeRide.Contracts.Responses.Bookings;

public sealed record BookingResponse(
    long BookingId,
    BookingType BookingType,
    BookingStatus BookingStatus,
    DateTime? ScheduledAt,
    decimal EstimatedFare,
    string Message);
