namespace SafeRide.Contracts.Responses.Drivers;

public sealed record NearbyDriverResponse(
    Guid DriverId,
    double Latitude,
    double Longitude);
