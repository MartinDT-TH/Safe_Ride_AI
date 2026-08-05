namespace SafeRide.Contracts.Requests.Trips;

public sealed record TriggerSOSRequest(
    double Latitude,
    double Longitude,
    string? Message);
