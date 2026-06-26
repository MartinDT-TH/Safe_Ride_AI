using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.BackgroundJobs;

public sealed class CleanupStaleDriverLocationJob
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDriverRealtimeService _driverRealtimeService;
    private readonly IDateTimeProvider _clock;
    private readonly IOptionsMonitor<CleanupStaleDriverLocationJobOptions> _options;
    private readonly ILogger<CleanupStaleDriverLocationJob> _logger;

    public CleanupStaleDriverLocationJob(
        ApplicationDbContext dbContext,
        IDriverRealtimeService driverRealtimeService,
        IDateTimeProvider clock,
        IOptionsMonitor<CleanupStaleDriverLocationJobOptions> options,
        ILogger<CleanupStaleDriverLocationJob> logger)
    {
        _dbContext = dbContext;
        _driverRealtimeService = driverRealtimeService;
        _clock = clock;
        _options = options;
        _logger = logger;
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
            .ToListAsync(cancellationToken);

        foreach (var driverId in staleDriverIds)
        {
            await _driverRealtimeService.SetDriverOfflineAsync(driverId, cancellationToken);
        }

        if (staleDriverIds.Count > 0)
        {
            _logger.LogInformation(
                "Cleaned up {DriverCount} stale online driver locations.",
                staleDriverIds.Count);
        }
    }
}
