namespace SafeRide.Contracts.Responses.Drivers;

public sealed record DriverMatchingPreferencesResponse(
    bool AcceptLongPickupTrips,
    bool AcceptLongDistanceTrips);
