namespace SafeRide.Infrastructure.Services;

public sealed class TripTrackingOptions
{
    public const string SectionName = "TripTracking";

    public int TripLiveTtlHours { get; set; } = 12;

    public int DriverStatusTtlMinutes { get; set; } = 5;

    public int TrackingTtlHours { get; set; } = 6;

    public int MaxPathPoints { get; set; } = 3000;

    public double AccumulatorJitterThresholdMeters { get; set; } = 5;

    public double PathSampleDistanceMeters { get; set; } = 25;

    public int PathSampleIntervalSeconds { get; set; } = 10;

    public double MaxInferredSpeedKmh { get; set; } = 130;

    public double MaxAccuracyMeters { get; set; } = 50;

    public int FinalizeLockSeconds { get; set; } = 30;

    public int MinFallbackPathPointCount { get; set; } = 2;

    public double MinTrustedDistanceMeters { get; set; } = 10;
}
