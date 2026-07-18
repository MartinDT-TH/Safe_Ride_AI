using SafeRide.Domain.Enums;

namespace SafeRide.Contracts.Responses.Drivers;

public sealed record DriverTripRequestResponse(
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
