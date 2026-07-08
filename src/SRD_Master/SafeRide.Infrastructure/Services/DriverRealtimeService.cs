using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Common.Realtime;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using SafeRide.Infrastructure.Simulator;
using System.Text.Json;

namespace SafeRide.Infrastructure.Services;

public sealed class DriverRealtimeService : IDriverRealtimeService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRedisService _redisService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IRealtimeNotificationService _realtimeNotificationService;
    private readonly IOptionsMonitor<DriverRealtimeOptions> _options;
    private readonly IOptionsMonitor<TripTrackingOptions> _tripTrackingOptions;
    private readonly ILogger<DriverRealtimeService> _logger;

    public DriverRealtimeService(
        ApplicationDbContext dbContext,
        IRedisService redisService,
        IDateTimeProvider dateTimeProvider,
        IRealtimeNotificationService realtimeNotificationService,
        IOptionsMonitor<DriverRealtimeOptions> options,
        IOptionsMonitor<TripTrackingOptions> tripTrackingOptions,
        ILogger<DriverRealtimeService> logger)
    {
        _dbContext = dbContext;
        _redisService = redisService;
        _dateTimeProvider = dateTimeProvider;
        _realtimeNotificationService = realtimeNotificationService;
        _options = options;
        _tripTrackingOptions = tripTrackingOptions;
        _logger = logger;
    }

    public async Task UpdateDriverLocationAsync(
        Guid driverId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        await UpdateDriverLocationAsync(
            driverId,
            new DriverLocationUpdateInput(latitude, longitude),
            cancellationToken);
    }

    public async Task UpdateDriverLocationAsync(
        Guid driverId,
        DriverLocationUpdateInput location,
        CancellationToken cancellationToken = default)
    {
        ValidateCoordinate(location.Latitude, location.Longitude);

        var utcNow = _dateTimeProvider.UtcNow;
        var activeTrip = await GetActiveTripForLocationAsync(
            driverId,
            cancellationToken);

        // If the simulator is enabled for real drivers, it will publish mock coordinates.
        // We must ignore the real device GPS updates during an active trip to avoid fighting the simulator.
        // if (activeTrip != null && true)
        // {
        //     return;
        // }

        await CacheDriverLocationAsync(
            driverId,
            location.Latitude,
            location.Longitude,
            utcNow,
            activeTrip is null ? DriverWorkStatus.Online : DriverWorkStatus.Busy);

        await RefreshDriverHeartbeatAsync(driverId, utcNow, cancellationToken);
        await RecordTripTrackingPointIfEligibleAsync(
            location,
            activeTrip,
            utcNow,
            cancellationToken);

        await _realtimeNotificationService.PublishDriverLocationUpdatedAsync(
            new DriverLocationUpdatedEvent(
                driverId,
                activeTrip?.CustomerId,
                activeTrip?.TripId,
                location.Latitude,
                location.Longitude,
                utcNow),
            cancellationToken);
    }

    public async Task SetDriverOnlineAsync(
        Guid driverId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        ValidateCoordinate(latitude, longitude);

        var utcNow = _dateTimeProvider.UtcNow;
        var profile = await _dbContext.DriverProfiles
            .FirstOrDefaultAsync(x => x.DriverId == driverId, cancellationToken);
        if (profile is not null && profile.WorkStatus != DriverWorkStatus.Busy)
        {
            profile.WorkStatus = DriverWorkStatus.Online;
            profile.LastActiveAt = utcNow;
            profile.UpdatedAt = utcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await CacheDriverLocationAsync(
            driverId,
            latitude,
            longitude,
            utcNow,
            profile?.WorkStatus == DriverWorkStatus.Busy
                ? DriverWorkStatus.Busy
                : DriverWorkStatus.Online);
    }

    public async Task SetDriverOfflineAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        var profile = await _dbContext.DriverProfiles
            .FirstOrDefaultAsync(x => x.DriverId == driverId, cancellationToken);
        if (profile is not null && profile.WorkStatus != DriverWorkStatus.Busy)
        {
            profile.WorkStatus = DriverWorkStatus.Offline;
            profile.LastActiveAt = utcNow;
            profile.UpdatedAt = utcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await TryRemoveRedisKeyAsync(RedisKeys.DriverOnline(driverId));
        await TryRemoveRedisKeyAsync(RedisKeys.DriverStatus(driverId));
        await TryRemoveRedisKeyAsync(RedisKeys.DriverLocation(driverId));
        if (profile?.WorkStatus != DriverWorkStatus.Busy)
        {
            await TryRemoveRedisKeyAsync(RedisKeys.DriverActiveTrip(driverId));
        }
        await TryRemoveRedisKeyAsync(RedisKeys.DriverHeartbeatThrottle(driverId));
        await TryRemoveDriverFromOnlineGeoAsync(driverId, cancellationToken);
    }

    public Task RemoveDriverFromOnlineGeoAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        return _redisService.GeoRemoveAsync(
            RedisKeys.OnlineDriversGeo,
            driverId.ToString(),
            cancellationToken);
    }

    private async Task CacheDriverLocationAsync(
        Guid driverId,
        double latitude,
        double longitude,
        DateTime utcNow,
        DriverWorkStatus workStatus)
    {
        var options = _options.CurrentValue;
        var driverLocationTtl = TimeSpan.FromMinutes(options.DriverLocationTtlMinutes);
        var driverOnlineTtl = TimeSpan.FromMinutes(options.DriverOnlineTtlMinutes);
        var cache = new DriverLocationCache(
            driverId,
            latitude,
            longitude,
            utcNow);

        await _redisService.SetAsync(
            RedisKeys.DriverLocation(driverId),
            JsonSerializer.Serialize(cache),
            driverLocationTtl);
        await _redisService.SetAsync(
            RedisKeys.DriverOnline(driverId),
            "1",
            driverOnlineTtl);
        await _redisService.SetAsync(
            RedisKeys.DriverStatus(driverId),
            workStatus.ToString(),
            driverOnlineTtl);
        await _redisService.GeoAddAsync(
            RedisKeys.OnlineDriversGeo,
            longitude,
            latitude,
            driverId.ToString());
    }

    private async Task RefreshDriverHeartbeatAsync(
        Guid driverId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var interval = TimeSpan.FromSeconds(
            _options.CurrentValue.DriverHeartbeatDbUpdateIntervalSeconds);
        var shouldUpdateHeartbeat = await _redisService.SetIfNotExistsAsync(
            RedisKeys.DriverHeartbeatThrottle(driverId),
            utcNow.Ticks.ToString(),
            interval);
        if (!shouldUpdateHeartbeat)
        {
            return;
        }

        var profile = await _dbContext.DriverProfiles
            .FirstOrDefaultAsync(x => x.DriverId == driverId, cancellationToken);
        if (profile is null)
        {
            return;
        }

        profile.LastActiveAt = utcNow;
        profile.UpdatedAt = utcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordTripTrackingPointIfEligibleAsync(
        DriverLocationUpdateInput location,
        ActiveDriverTripSnapshot? activeTrip,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (activeTrip is null || activeTrip.TripStatus != TripStatus.IN_PROGRESS)
        {
            return;
        }

        var clientTimestampUtc = NormalizeClientTimestamp(location.ClientTimestampUtc);
        var effectiveTimestampUtc = clientTimestampUtc ?? utcNow;
        var point = new TripTrackingPoint(
            activeTrip.TripId,
            location.Latitude,
            location.Longitude,
            new DateTimeOffset(utcNow).ToUnixTimeMilliseconds(),
            new DateTimeOffset(effectiveTimestampUtc).ToUnixTimeMilliseconds(),
            utcNow,
            clientTimestampUtc,
            location.Sequence,
            location.AccuracyMeters,
            location.SpeedMetersPerSecond);

        var options = _tripTrackingOptions.CurrentValue;
        var writeOptions = new TripTrackingWriteOptions(
            TimeSpan.FromHours(options.TrackingTtlHours),
            options.MaxPathPoints,
            options.AccumulatorJitterThresholdMeters,
            options.PathSampleDistanceMeters,
            options.PathSampleIntervalSeconds,
            options.MaxInferredSpeedKmh,
            options.MaxAccuracyMeters);

        try
        {
            await _redisService.RecordTripTrackingPointAsync(
                point,
                writeOptions,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to record trip tracking point for trip {TripId}. Realtime location publish will continue.",
                activeTrip.TripId);
        }
    }

    private async Task<ActiveDriverTripSnapshot?> GetActiveTripForLocationAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var cached = await _redisService.GetAsync(RedisKeys.DriverActiveTrip(driverId));
        if (!string.IsNullOrWhiteSpace(cached))
        {
            try
            {
                var cache = JsonSerializer.Deserialize<DriverActiveTripCache>(cached);
                if (cache is not null && IsActiveTripStatus(cache.TripStatus))
                {
                    return new ActiveDriverTripSnapshot(
                        cache.TripId,
                        cache.BookingId,
                        cache.CustomerId,
                        cache.TripStatus,
                        cache.DriverAssignedAt);
                }
            }
            catch (JsonException)
            {
                await _redisService.RemoveAsync(RedisKeys.DriverActiveTrip(driverId));
            }
        }

        var activeTrip = await _dbContext.Trips
            .AsNoTracking()
            .Where(x => x.DriverId == driverId
                && x.TripStatus != TripStatus.COMPLETED
                && x.TripStatus != TripStatus.CANCELLED)
            .Select(x => new ActiveDriverTripSnapshot(
                x.Id,
                x.BookingId,
                x.Booking.CustomerId,
                x.TripStatus,
                x.DriverAssignedAt ?? x.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (activeTrip is not null)
        {
            var cache = new DriverActiveTripCache(
                activeTrip.TripId,
                activeTrip.BookingId,
                driverId,
                activeTrip.CustomerId,
                activeTrip.TripStatus,
                activeTrip.DriverAssignedAt);
            await _redisService.SetAsync(
                RedisKeys.DriverActiveTrip(driverId),
                JsonSerializer.Serialize(cache),
                TimeSpan.FromMinutes(_options.CurrentValue.DriverOnlineTtlMinutes));
        }

        return activeTrip;
    }

    private static bool IsActiveTripStatus(TripStatus status)
    {
        return status is not TripStatus.COMPLETED and not TripStatus.CANCELLED;
    }

    private async Task TryRemoveRedisKeyAsync(string key)
    {
        try
        {
            await _redisService.RemoveAsync(key);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Redis cleanup failed for key {RedisKey} while setting driver offline.",
                key);
        }
    }

    private async Task TryRemoveDriverFromOnlineGeoAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        try
        {
            await RemoveDriverFromOnlineGeoAsync(driverId, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Redis GEO cleanup failed while setting driver {DriverId} offline.",
                driverId);
        }
    }

    private static void ValidateCoordinate(double latitude, double longitude)
    {
        if (!double.IsFinite(latitude)
            || !double.IsFinite(longitude)
            || latitude is < -90 or > 90
            || longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude),
                "Driver location coordinates are invalid.");
        }
    }

    private static DateTime? NormalizeClientTimestamp(DateTime? timestamp)
    {
        if (!timestamp.HasValue)
        {
            return null;
        }

        return timestamp.Value.Kind switch
        {
            DateTimeKind.Utc => timestamp.Value,
            DateTimeKind.Local => timestamp.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestamp.Value, DateTimeKind.Utc)
        };
    }

    private sealed record ActiveDriverTripSnapshot(
        long TripId,
        long BookingId,
        Guid CustomerId,
        TripStatus TripStatus,
        DateTime DriverAssignedAt);
}
