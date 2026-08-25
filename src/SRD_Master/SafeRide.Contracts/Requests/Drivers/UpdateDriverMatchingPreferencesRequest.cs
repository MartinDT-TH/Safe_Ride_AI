namespace SafeRide.Contracts.Requests.Drivers;

public sealed record UpdateDriverMatchingPreferencesRequest(
    bool AcceptLongPickupTrips,
    bool AcceptLongDistanceTrips);
