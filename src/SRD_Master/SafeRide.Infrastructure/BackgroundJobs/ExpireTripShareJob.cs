using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.TripSharing;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.BackgroundJobs;

public sealed class ExpireTripShareJob
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRealtimeNotificationService _realtime;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ExpireTripShareJob> _logger;

    public ExpireTripShareJob(
        ApplicationDbContext dbContext,
        IRealtimeNotificationService realtime,
        IDateTimeProvider clock,
        ILogger<ExpireTripShareJob> logger)
    {
        _dbContext = dbContext;
        _realtime = realtime;
        _clock = clock;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        long tripShareId,
        long expectedExpiresAtTicks,
        CancellationToken cancellationToken = default)
    {
        var share = await _dbContext.TripShares
            .AsNoTracking()
            .Where(x => x.Id == tripShareId)
            .Select(x => new
            {
                x.Id,
                x.ExpiresAt,
                x.RevokedAt,
                x.Trip.TripStatus
            })
            .FirstOrDefaultAsync(cancellationToken);
        var utcNow = _clock.UtcNow;
        if (share is null
            || share.RevokedAt.HasValue
            || share.ExpiresAt.Ticks != expectedExpiresAtTicks
            || share.ExpiresAt > utcNow)
        {
            _logger.LogDebug(
                "Skipped stale or inactive TripShare expiration job for TripShareId={TripShareId}.",
                tripShareId);
            return;
        }

        await _realtime.PublishSharedTripStatusAsync(
            new SharedTripStatusUpdate(share.Id, share.TripStatus.ToString(), utcNow),
            "TripShareExpired",
            cancellationToken);
    }
}
