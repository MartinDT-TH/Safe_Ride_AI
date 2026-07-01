namespace SafeRide.Infrastructure.Services;

public sealed class TripTrackingOptions
{
    public const string SectionName = "TripTracking";

    public int TripLiveTtlHours { get; set; } = 12;

    public int DriverStatusTtlMinutes { get; set; } = 5;
}
