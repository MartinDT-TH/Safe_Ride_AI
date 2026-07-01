using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Drivers.DTOs;

public sealed record ActiveDriverTripDto(
    long BookingId,
    long TripId,
    TripStatus TripStatus,
    double PickupLat,
    double PickupLng,
    double? DestLat,
    double? DestLng,
    string? EncodedPolyline);
