namespace SafeRide.Infrastructure.Redis;

public sealed record TripTrackingPoint(
    long TripId,
    double Latitude,
    double Longitude,
    long ServerTimestampUnixMs,
    long EffectiveTimestampUnixMs,
    DateTime ServerTimestampUtc,
    DateTime? ClientTimestampUtc = null,
    long? Sequence = null,
    double? AccuracyMeters = null,
    double? SpeedMetersPerSecond = null);

public sealed record TripTrackingWriteOptions(
    TimeSpan Ttl,
    int MaxPathPoints,
    double JitterThresholdMeters,
    double PathSampleDistanceMeters,
    int PathSampleIntervalSeconds,
    double MaxInferredSpeedKmh,
    double MaxAccuracyMeters);

public sealed record TripTrackingUpdateResult(
    bool Accepted,
    bool AppendedToPath,
    double SegmentDistanceMeters,
    double TotalDistanceMeters,
    string Reason);

public sealed record TripTrackingSnapshot(
    IReadOnlyList<TripTrackingPoint> PathPoints,
    double DistanceMeters,
    TripTrackingPoint? FirstAcceptedPoint,
    TripTrackingPoint? LastAcceptedPoint,
    DateTime? TrackingStartedAtUtc,
    DateTime? LastUpdatedAtUtc);

internal sealed record TripTrackingMetadata(
    string? FirstAcceptedPointJson,
    int AcceptedCount,
    int RejectedCount,
    long? TrackingStartedUnixMs,
    long? LastUpdatedUnixMs);
