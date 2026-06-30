namespace SafeRide.Infrastructure.Authentication;

public sealed class RefreshTokenCleanupOptions
{
    public const string SectionName = "RefreshTokens";

    public int CleanupRetentionDays { get; init; } = 7;

    public int CleanupBatchSize { get; init; } = 500;

    public string CronExpression { get; init; } = Hangfire.Cron.Daily();
}
