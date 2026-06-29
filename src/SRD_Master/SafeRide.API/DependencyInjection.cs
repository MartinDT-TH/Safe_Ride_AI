using Hangfire;
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
            .ValidateOnStart();

        return services;
    }

    public static WebApplication UseSafeRideApiJobs(this WebApplication app)
    {
        if (app.Environment.IsEnvironment("Testing"))
        {
            return app;
        }

        // var recurringJobs = app.Services.GetRequiredService<IRecurringJobManager>();

        // recurringJobs.AddOrUpdate<CleanupStaleDriverLocationJob>(
        //     "cleanup-stale-driver-location",
        //     job => job.ExecuteAsync(CancellationToken.None),
        //     Cron.Minutely());

        // recurringJobs.AddOrUpdate<CleanupExpiredRefreshTokensJob>(
        //     "cleanup-expired-refresh-tokens",
        //     job => job.ExecuteAsync(CancellationToken.None),
        //     Cron.Daily());

        return app;
    }
}
