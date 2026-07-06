using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Drivers.DTOs;
using SafeRide.Application.Features.Trips.DTOs;
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

    public async Task<ActiveDriverTripDto?> GetActiveTripAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var trip = await _dbContext.Trips
            .AsNoTracking()
            .Include(trip => trip.Booking)
            .Include(trip => trip.ReturnConfirmations)
            .ThenInclude(returnConfirmation => returnConfirmation.Evidence)

            .Where(trip => trip.DriverId == driverId
                && (trip.TripStatus == TripStatus.ACCEPTED
                    || trip.TripStatus == TripStatus.DRIVER_ARRIVING
                    || trip.TripStatus == TripStatus.ARRIVED
                    || trip.TripStatus == TripStatus.IN_PROGRESS
                    || trip.TripStatus == TripStatus.WAITING_RETURN_CONFIRM
                    || trip.TripStatus == TripStatus.RETURN_CONFIRMED))
            .OrderByDescending(trip => trip.DriverAssignedAt ?? trip.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (trip is null)
        {
            return null;
        }

        var confirmation = trip.ReturnConfirmations
            .OrderByDescending(returnConfirmation => returnConfirmation.ConfirmedAt)
            .ThenByDescending(returnConfirmation => returnConfirmation.Id)
            .FirstOrDefault();

        return new ActiveDriverTripDto(
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
            trip.Booking.RoutePolyline,
            confirmation is null
                ? null
                : new TripReturnConfirmationSummaryDto(
                    confirmation.Id,
                    confirmation.HandoverStatus,
                    confirmation.DriverId,
                    confirmation.ConfirmedByUserId,
                    confirmation.ConfirmedAt,
                    confirmation.DriverLatitude,
                    confirmation.DriverLongitude,
                    confirmation.Note,
                    confirmation.Evidence
                        .OrderBy(evidence => evidence.DisplayOrder)
                        .Select(evidence => new TripReturnEvidenceSummaryDto(
                            evidence.Id,
                            evidence.ImageUrl,
                            evidence.ContentType,
                            evidence.DisplayOrder))
                        .ToList()));
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
