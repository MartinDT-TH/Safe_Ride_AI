using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.BackgroundJobs;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.Infrastructure.Services;

/// <summary>
/// Hangfire-backed implementation of <see cref="IBookingLifecycleJobScheduler"/>.
/// Schedules delayed jobs for booking radius expansion and expiry,
/// and persists the resulting job IDs in Redis so they can be cancelled later.
/// </summary>
public sealed class HangfireBookingLifecycleJobScheduler : IBookingLifecycleJobScheduler
{
    private readonly IBackgroundJobClient _jobClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IRedisService _redisService;
    private readonly IOptionsMonitor<BookingLifecycleJobSchedulerOptions> _options;
    private readonly ILogger<HangfireBookingLifecycleJobScheduler> _logger;

    public HangfireBookingLifecycleJobScheduler(
        IBackgroundJobClient jobClient,
        IServiceScopeFactory scopeFactory,
        IRedisService redisService,
        IOptionsMonitor<BookingLifecycleJobSchedulerOptions> options,
        ILogger<HangfireBookingLifecycleJobScheduler> logger)
    {
        _jobClient = jobClient;
        _scopeFactory = scopeFactory;
        _redisService = redisService;
        _options = options;
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
            GetJobIdTtl());

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
            GetJobIdTtl());

        _logger.LogInformation(
            "Scheduled ExpireSearchingBookingsJob for BookingId={BookingId} in {Delay}. HangfireJobId={JobId}",
            bookingId, delay, jobId);
    }

    public void ScheduleExpireDriverOffer(long offerId, TimeSpan delay)
    {
        var jobId = _jobClient.Schedule<ExpireDriverOfferJob>(
            job => job.ExecuteAsync(offerId, CancellationToken.None),
            delay);

        _ = _redisService.SetAsync(
            RedisKeys.HangfireExpireDriverOfferJobId(offerId),
            jobId,
            GetJobIdTtl());

        _logger.LogInformation(
            "Scheduled ExpireDriverOfferJob for OfferId={OfferId} in {Delay}. HangfireJobId={JobId}",
            offerId, delay, jobId);
    }

    public Task CancelExpireDriverOfferAsync(
        long offerId,
        CancellationToken cancellationToken = default)
    {
        return CancelJobAsync(
            RedisKeys.HangfireExpireDriverOfferJobId(offerId),
            offerId,
            "ExpireDriverOfferJob");
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

        var openOfferIds = await GetOpenOfferIdsAsync(bookingId, cancellationToken);
        foreach (var offerId in openOfferIds)
        {
            await CancelExpireDriverOfferAsync(offerId, cancellationToken);
        }
    }

    private async Task CancelJobAsync(string redisKey, long entityId, string jobName)
    {
        var jobId = await _redisService.GetAsync(redisKey);
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return;
        }

        var deleted = _jobClient.Delete(jobId);
        await _redisService.RemoveAsync(redisKey);

        _logger.LogInformation(
            "Cancelled {JobName} for EntityId={EntityId}. HangfireJobId={JobId} Deleted={Deleted}",
            jobName, entityId, jobId, deleted);
    }

    private async Task<IReadOnlyList<long>> GetOpenOfferIdsAsync(
        long bookingId,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await dbContext.BookingDriverOffers
            .AsNoTracking()
            .Where(x => x.BookingId == bookingId
                && (x.OfferStatus == DriverOfferStatus.Sent
                    || x.OfferStatus == DriverOfferStatus.DriverAccepted))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    private TimeSpan GetJobIdTtl()
    {
        return TimeSpan.FromHours(Math.Max(1, _options.CurrentValue.JobIdTtlHours));
    }
}
