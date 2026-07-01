using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using System.Text.Json;

namespace SafeRide.Infrastructure.BackgroundJobs;

public sealed class CleanupStaleDriverLocationJob
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDriverRealtimeService _driverRealtimeService;
    private readonly IDateTimeProvider _clock;
    private readonly IOptionsMonitor<CleanupStaleDriverLocationJobOptions> _options;
    private readonly ILogger<CleanupStaleDriverLocationJob> _logger;
    private readonly IRedisService _redisService;

    public CleanupStaleDriverLocationJob(
        ApplicationDbContext dbContext,
        IDriverRealtimeService driverRealtimeService,
        IDateTimeProvider clock,
        IOptionsMonitor<CleanupStaleDriverLocationJobOptions> options,
        ILogger<CleanupStaleDriverLocationJob> logger,
        IRedisService redisService)
    {
        _dbContext = dbContext;
        _driverRealtimeService = driverRealtimeService;
        _clock = clock;
        _options = options;
        _logger = logger;
        _redisService = redisService;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var staleAfter = TimeSpan.FromMinutes(_options.CurrentValue.StaleAfterMinutes);
        var cutoff = _clock.UtcNow.Subtract(staleAfter);

        var staleDriverIds = await _dbContext.DriverProfiles
            .AsNoTracking()
            .Where(profile => profile.WorkStatus == DriverWorkStatus.Online
                && (profile.LastActiveAt == null || profile.LastActiveAt <= cutoff)
                && !_dbContext.Trips.Any(trip => trip.DriverId == profile.DriverId
                    && (trip.TripStatus == TripStatus.ACCEPTED
                        || trip.TripStatus == TripStatus.DRIVER_ARRIVING
                        || trip.TripStatus == TripStatus.ARRIVED
                        || trip.TripStatus == TripStatus.IN_PROGRESS)))
            .Select(profile => profile.DriverId)
            .Take(_options.CurrentValue.BatchSize)
            .ToListAsync(cancellationToken);

        int skippedFreshCount = 0;
        int cleanedOfflineCount = 0;

        foreach (var driverId in staleDriverIds)
        {
            var locationJson = await _redisService.GetAsync(RedisKeys.DriverLocation(driverId));
            if (!string.IsNullOrEmpty(locationJson))
            {
                var cache = JsonSerializer.Deserialize<DriverLocationCache>(locationJson);
                if (cache is not null && cache.UpdatedAt > cutoff)
                {
                    // Redis cache is fresh, do not set offline. Refresh DB instead.
                    var profile = await _dbContext.DriverProfiles
                        .FirstOrDefaultAsync(p => p.DriverId == driverId, cancellationToken);
                    if (profile != null)
                    {
                        profile.LastActiveAt = cache.UpdatedAt;
                        profile.UpdatedAt = _clock.UtcNow;
                        await _dbContext.SaveChangesAsync(cancellationToken);
                    }
                    
                    skippedFreshCount++;
                    continue;
                }
            }

            // Redis cache is missing or stale. Set offline.
            await _driverRealtimeService.SetDriverOfflineAsync(driverId, cancellationToken);
            cleanedOfflineCount++;
        }

        _logger.LogInformation(
            "CleanupStaleDriverLocationJob completed. Total candidates checked: {CandidatesChecked}, Skipped (Redis fresh): {SkippedFreshCount}, Cleaned offline (missing/stale): {CleanedOfflineCount}.",
            staleDriverIds.Count,
            skippedFreshCount,
            cleanedOfflineCount);
    }
}
