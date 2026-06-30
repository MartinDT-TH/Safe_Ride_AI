namespace SafeRide.Infrastructure.BackgroundJobs;

public sealed class ExpandSearchingRadiusJobOptions
{
    public const string SectionName = "BackgroundJobs:ExpandSearchingRadius";

    public int RadiusExpandedNotificationTtlMinutes { get; set; } = 15;
}

public sealed class CleanupStaleDriverLocationJobOptions
{
    public const string SectionName = "BackgroundJobs:CleanupStaleDriverLocation";

    public int StaleAfterMinutes { get; set; } = 5;

    public int BatchSize { get; set; } = 500;

    public string CronExpression { get; set; } = Hangfire.Cron.Minutely();
}

public sealed class BookingLifecycleJobSchedulerOptions
{
    public const string SectionName = "BackgroundJobs:BookingLifecycleJobScheduler";

    public int JobIdTtlHours { get; set; } = 2;
}
