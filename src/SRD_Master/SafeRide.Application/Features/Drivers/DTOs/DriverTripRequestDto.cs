using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Drivers.DTOs;

public sealed record DriverTripRequestDto(
    long OfferId,
    long BookingId,
    DriverOfferStatus OfferStatus,
    DateTime ExpiresAt,
    decimal ExpectedIncome,
    string PickupAddress,
    string? DestinationAddress,
    double? PickupDistanceKm = null,
    int? PickupDurationMinutes = null,
    int? CustomerConfirmRemainingSeconds = null);
