using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Drivers.DTOs;
using SafeRide.Contracts.Responses.Drivers;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using System.Text.Json;

namespace SafeRide.Infrastructure.Services;

public sealed class DriverQueryService : IDriverQueryService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRedisService _redisService;

    public DriverQueryService(
        ApplicationDbContext dbContext,
        IRedisService redisService)
    {
        _dbContext = dbContext;
        _redisService = redisService;
    }

    public async Task<IReadOnlyList<NearbyDriverResponse>> GetNearbyDriversAsync(
        double latitude,
        double longitude,
        double radiusKm,
        int limit,
        CancellationToken cancellationToken)
    {
        var driverIds = await _redisService.GeoRadiusAsync(
            RedisKeys.OnlineDriversGeo,
            longitude,
            latitude,
            radiusKm,
            limit);

        var tasks = driverIds.Select(async id =>
        {
            var guid = Guid.Parse(id);
            var locationJson = await _redisService.GetAsync(RedisKeys.DriverLocation(guid));
            if (string.IsNullOrEmpty(locationJson))
            {
                return null;
            }

            var cache = JsonSerializer.Deserialize<DriverLocationCache>(locationJson);
            return cache is null
                ? null
                : new NearbyDriverResponse(
                    guid,
                    cache.Latitude,
                    cache.Longitude);
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(x => x is not null).ToList()!;
    }

    public Task<ActiveDriverTripDto?> GetActiveTripAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Trips
            .AsNoTracking()
            .Where(trip => trip.DriverId == driverId
                && (trip.TripStatus == TripStatus.ACCEPTED
                    || trip.TripStatus == TripStatus.DRIVER_ARRIVING
                    || trip.TripStatus == TripStatus.ARRIVED
                    || trip.TripStatus == TripStatus.IN_PROGRESS))
            .OrderByDescending(trip => trip.DriverAssignedAt ?? trip.CreatedAt)
            .Select(trip => new ActiveDriverTripDto(
                trip.BookingId,
                trip.Id,
                trip.TripStatus,
                trip.Booking.PickupLocation.Y,
                trip.Booking.PickupLocation.X,
                trip.Booking.DestinationLocation != null
                    ? trip.Booking.DestinationLocation.Y
                    : (double?)null,
                trip.Booking.DestinationLocation != null
                    ? trip.Booking.DestinationLocation.X
                    : (double?)null,
                trip.Booking.RoutePolyline))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> HasActiveTripOrBusyStatusAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var isBusy = await _dbContext.DriverProfiles
            .Where(p => p.DriverId == driverId)
            .Select(p => p.WorkStatus == DriverWorkStatus.Busy)
            .FirstOrDefaultAsync(cancellationToken);

        if (isBusy)
        {
            return true;
        }

        return await _dbContext.Trips
            .AnyAsync(trip => trip.DriverId == driverId
                && (trip.TripStatus == TripStatus.ACCEPTED
                    || trip.TripStatus == TripStatus.DRIVER_ARRIVING
                    || trip.TripStatus == TripStatus.ARRIVED
                    || trip.TripStatus == TripStatus.IN_PROGRESS),
                cancellationToken);
    }
}
