using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Drivers.DTOs;

public sealed record DriverTripRequestDto(
    long OfferId,
    long BookingId,
    DriverOfferStatus OfferStatus,
    DateTime ExpiresAt,
    string PickupAddress,
    string? DestinationAddress,
    double? PickupDistanceKm = null,
    int? PickupDurationMinutes = null,
    int? CustomerConfirmRemainingSeconds = null,
    decimal? LongPickupCompensation = null,
    bool IsLongPickup = false,
    bool IsLongDistanceTrip = false);
