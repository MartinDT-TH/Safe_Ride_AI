using Hangfire;
using Microsoft.Extensions.Logging;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Infrastructure.BackgroundJobs;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.Infrastructure.Services;

/// <summary>
/// Hangfire-backed implementation of <see cref="IBookingLifecycleJobScheduler"/>.
/// Schedules delayed jobs for booking radius expansion and expiry,
/// and persists the resulting job IDs in Redis so they can be cancelled later.
/// </summary>
public sealed class HangfireBookingLifecycleJobScheduler : IBookingLifecycleJobScheduler
{
    private static readonly TimeSpan JobIdTtl = TimeSpan.FromHours(2);

    private readonly IBackgroundJobClient _jobClient;
    private readonly IRedisService _redisService;
    private readonly ILogger<HangfireBookingLifecycleJobScheduler> _logger;

    public HangfireBookingLifecycleJobScheduler(
        IBackgroundJobClient jobClient,
        IRedisService redisService,
        ILogger<HangfireBookingLifecycleJobScheduler> logger)
    {
        _jobClient = jobClient;
        _redisService = redisService;
        _logger = logger;
    }

    public void ScheduleExpandRadius(long bookingId, TimeSpan delay)
    {
        var jobId = _jobClient.Schedule<ExpandSearchingRadiusJob>(
            job => job.ExecuteAsync(bookingId, CancellationToken.None),
            delay);

        // Fire-and-forget: best effort save. If Redis is down we lose the ability
        // to cancel, but the job itself is still idempotent and will no-op if the
        // booking has already left Searching state by the time it fires.
        _ = _redisService.SetAsync(
            RedisKeys.HangfireExpandRadiusJobId(bookingId),
            jobId,
            JobIdTtl);

        _logger.LogInformation(
            "Scheduled ExpandSearchingRadiusJob for BookingId={BookingId} in {Delay}. HangfireJobId={JobId}",
            bookingId, delay, jobId);
    }

    public void ScheduleExpireBooking(long bookingId, TimeSpan delay)
    {
        var jobId = _jobClient.Schedule<ExpireSearchingBookingsJob>(
            job => job.ExecuteAsync(bookingId, CancellationToken.None),
            delay);

        _ = _redisService.SetAsync(
            RedisKeys.HangfireExpireBookingJobId(bookingId),
            jobId,
            JobIdTtl);

        _logger.LogInformation(
            "Scheduled ExpireSearchingBookingsJob for BookingId={BookingId} in {Delay}. HangfireJobId={JobId}",
            bookingId, delay, jobId);
    }

    public async Task CancelJobsForBookingAsync(long bookingId, CancellationToken cancellationToken = default)
    {
        await CancelJobAsync(
            RedisKeys.HangfireExpandRadiusJobId(bookingId),
            bookingId,
            "ExpandSearchingRadiusJob");

        await CancelJobAsync(
            RedisKeys.HangfireExpireBookingJobId(bookingId),
            bookingId,
            "ExpireSearchingBookingsJob");
    }

    private async Task CancelJobAsync(string redisKey, long bookingId, string jobName)
    {
        var jobId = await _redisService.GetAsync(redisKey);
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return;
        }

        var deleted = _jobClient.Delete(jobId);
        await _redisService.RemoveAsync(redisKey);

        _logger.LogInformation(
            "Cancelled {JobName} for BookingId={BookingId}. HangfireJobId={JobId} Deleted={Deleted}",
            jobName, bookingId, jobId, deleted);
    }
}
