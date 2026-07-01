namespace SafeRide.Infrastructure.Services;

public sealed class DriverRealtimeOptions
{
    public const string SectionName = "DriverRealtime";

    public int DriverLocationTtlMinutes { get; set; } = 60;

    public int DriverOnlineTtlMinutes { get; set; } = 60;

    public int DriverHeartbeatDbUpdateIntervalSeconds { get; set; } = 60;
}
