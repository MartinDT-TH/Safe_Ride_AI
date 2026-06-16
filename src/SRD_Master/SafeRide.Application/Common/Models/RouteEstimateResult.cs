namespace SafeRide.Application.Common.Models;

public sealed record RouteEstimateResult(
    double DistanceKm,
    int DurationMinutes,
    string EncodedPolyline);
