using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Bookings.DTOs;

public sealed record BookingHistoryItemDto(
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
    string? DriverAvatarUrl,
    bool HasReported);
