namespace SafeRide.Application.Features.Drivers.DTOs;

public sealed record DriverMatchingPreferencesDto(
    bool AcceptLongPickupTrips,
    bool AcceptLongDistanceTrips);
