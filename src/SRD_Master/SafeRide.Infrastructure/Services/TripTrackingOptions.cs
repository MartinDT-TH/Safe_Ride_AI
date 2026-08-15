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

    public decimal MinimumEarlyEndFare { get; set; } = 2_000m;

    public double RouteDeviationThresholdMeters { get; set; } = 100;

    public int RouteDeviationRequiredSamples { get; set; } = 3;

    public int RouteDeviationStateTtlMinutes { get; set; } = 2;

    public int RouteRerouteCooldownSeconds { get; set; } = 15;

    public int CustomerDeviationAlertCooldownMinutes { get; set; } = 5;

    public int ActiveRouteTtlHours { get; set; } = 4;

    public double ReverseProgressThresholdMeters { get; set; } = 150;

    public int ReverseRequiredSamples { get; set; } = 2;

    public double CustomerAlertDistanceIncreaseMeters { get; set; } = 500;
}
