using Hangfire;
using Microsoft.Extensions.Logging;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Infrastructure.BackgroundJobs;

namespace SafeRide.Infrastructure.Services;

public sealed class HangfireTripShareExpiryScheduler : ITripShareExpiryScheduler
{
    private readonly IBackgroundJobClient _jobClient;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<HangfireTripShareExpiryScheduler> _logger;

    public HangfireTripShareExpiryScheduler(
        IBackgroundJobClient jobClient,
        IDateTimeProvider clock,
        ILogger<HangfireTripShareExpiryScheduler> logger)
    {
        _jobClient = jobClient;
        _clock = clock;
        _logger = logger;
    }

    public void ScheduleExpiration(long tripShareId, DateTime expiresAt)
    {
        var delay = expiresAt - _clock.UtcNow;
        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }

        var jobId = _jobClient.Schedule<ExpireTripShareJob>(
            job => job.ExecuteAsync(tripShareId, expiresAt.Ticks, CancellationToken.None),
            delay);
        _logger.LogInformation(
            "Scheduled TripShare expiration for TripShareId={TripShareId} at {ExpiresAt}. HangfireJobId={JobId}",
            tripShareId,
            expiresAt,
            jobId);
    }
}
