namespace SafeRide.Application.Common.Models;

public sealed record TripArrivalVerificationResult(
    double Latitude,
    double Longitude,
    decimal DistanceMeters,
    DateTime VerifiedAt);
