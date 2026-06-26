using SafeRide.Domain.Enums;

namespace SafeRide.Contracts.Responses.Bookings;

public sealed record BookingHistoryResponse(
    long Id,
    string PickupAddress,
    string DestinationAddress,
    DateTime OccurredAt,
    double EstimatedDistanceKm,
    decimal EstimatedFare,
    decimal FinalFare,
    BookingStatus BookingStatus,
    string VehicleName,
    bool IsMotorbike,
    string? DriverName,
    double? DriverRating,
    string? DriverAvatarUrl);
