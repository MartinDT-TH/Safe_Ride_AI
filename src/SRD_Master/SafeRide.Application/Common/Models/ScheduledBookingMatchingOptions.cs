namespace SafeRide.Application.Common.Models;

public sealed class ScheduledBookingMatchingOptions
{
    public const string SectionName = "BackgroundJobs:ScheduledBookingMatching";

    public int StartMatchingBeforeMinutes { get; set; } = 15;

    public int PollingIntervalSeconds { get; set; } = 60;
}
