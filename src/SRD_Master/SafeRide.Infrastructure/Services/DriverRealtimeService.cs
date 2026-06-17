using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using System.Text.Json;

namespace SafeRide.Infrastructure.Services;

public sealed class DriverRealtimeService : IDriverRealtimeService
{
    private static readonly TimeSpan DriverLocationTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DriverOnlineTtl = TimeSpan.FromMinutes(5);

    private readonly ApplicationDbContext _dbContext;
    private readonly IRedisService _redisService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IRealtimeNotificationService _realtimeNotificationService;

    public DriverRealtimeService(
        ApplicationDbContext dbContext,
        IRedisService redisService,
        IDateTimeProvider dateTimeProvider,
        IRealtimeNotificationService realtimeNotificationService)
    {
        _dbContext = dbContext;
        _redisService = redisService;
        _dateTimeProvider = dateTimeProvider;
        _realtimeNotificationService = realtimeNotificationService;
    }

    public async Task UpdateDriverLocationAsync(
        Guid driverId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
    {
        ValidateCoordinate(latitude, longitude);

        var utcNow = _dateTimeProvider.UtcNow;
        await CacheDriverLocationAsync(
            driverId,
            latitude,
            longitude,
            utcNow);

        var activeTrip = await _dbContext.Trips
            .AsNoTracking()
            .Where(x => x.DriverId == driverId
                && (x.TripStatus == TripStatus.ACCEPTED
                    || x.TripStatus == TripStatus.DRIVER_ARRIVING
                    || x.TripStatus == TripStatus.ARRIVED
                    || x.TripStatus == TripStatus.IN_PROGRESS))
            .Select(x => new
            {
                x.Id,
                x.Booking.CustomerId
            })
            .FirstOrDefaultAsync(cancellationToken);

        await _realtimeNotificationService.PublishDriverLocationUpdatedAsync(
            new DriverLocationUpdatedEvent(
                driverId,
                activeTrip?.CustomerId,
                activeTrip?.Id,
                latitude,
                longitude,
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
            utcNow);
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

        await _redisService.RemoveAsync(RedisKeys.DriverOnline(driverId));
        await _redisService.RemoveAsync(RedisKeys.DriverStatus(driverId));
    }

    private async Task CacheDriverLocationAsync(
        Guid driverId,
        double latitude,
        double longitude,
        DateTime utcNow)
    {
        var cache = new DriverLocationCache(
            driverId,
            latitude,
            longitude,
            utcNow);

        await _redisService.SetAsync(
            RedisKeys.DriverLocation(driverId),
            JsonSerializer.Serialize(cache),
            DriverLocationTtl);
        await _redisService.SetAsync(
            RedisKeys.DriverOnline(driverId),
            "1",
            DriverOnlineTtl);
        await _redisService.SetAsync(
            RedisKeys.DriverStatus(driverId),
            DriverWorkStatus.Online.ToString(),
            DriverOnlineTtl);
        await _redisService.GeoAddAsync(
            RedisKeys.OnlineDriversGeo,
            longitude,
            latitude,
            driverId.ToString());
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
}
