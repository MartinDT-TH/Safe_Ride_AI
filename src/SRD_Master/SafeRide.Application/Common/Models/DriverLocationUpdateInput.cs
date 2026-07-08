namespace SafeRide.Application.Common.Models;

public sealed record DriverLocationUpdateInput(
    double Latitude,
    double Longitude,
    DateTime? ClientTimestampUtc = null,
    long? Sequence = null,
    double? AccuracyMeters = null,
    double? SpeedMetersPerSecond = null);
