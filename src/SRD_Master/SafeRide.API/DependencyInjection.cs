using Hangfire;
using Microsoft.Extensions.Options;
using SafeRide.Infrastructure.Authentication;
using SafeRide.Infrastructure.BackgroundJobs;

namespace SafeRide.API;

public static class DependencyInjection
{
    public static IServiceCollection AddSafeRideApiJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<RefreshTokenCleanupOptions>()
            .Bind(configuration.GetSection(RefreshTokenCleanupOptions.SectionName))
            .Validate(options => options.CleanupRetentionDays >= 0,
                "RefreshTokens:CleanupRetentionDays must be zero or greater.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.CronExpression),
                "RefreshTokens:CronExpression must be configured.")
            .ValidateOnStart();

        return services;
    }

    public static WebApplication UseSafeRideApiJobs(this WebApplication app)
    {
        if (app.Environment.IsEnvironment("Testing"))
        {
            return app;
        }

        var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();
        var staleDriverLocationOptions = app.Services
            .GetRequiredService<IOptions<CleanupStaleDriverLocationJobOptions>>()
            .Value;
        var refreshTokenCleanupOptions = app.Services
            .GetRequiredService<IOptions<RefreshTokenCleanupOptions>>()
            .Value;

        recurringJobs.AddOrUpdate<CleanupStaleDriverLocationJob>(
            "cleanup-stale-driver-location",
            job => job.ExecuteAsync(CancellationToken.None),
            staleDriverLocationOptions.CronExpression);

        recurringJobs.AddOrUpdate<CleanupExpiredRefreshTokensJob>(
            "cleanup-expired-refresh-tokens",
            job => job.ExecuteAsync(CancellationToken.None),
            refreshTokenCleanupOptions.CronExpression);

        return app;
    }
}
