using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Common.Realtime;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.Infrastructure.Simulator;

/// <summary>
/// Background service that automatically accepts driver offers for mock drivers.
/// Simulates real drivers responding to booking offers and moving.
/// </summary>
public sealed class MockDriverOfferAcceptorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MockDriverOfferAcceptorService> _logger;
    private readonly IOptionsMonitor<SimulatorOptions> _simulatorOptionsMonitor;
    private readonly List<MockDriver> _mockDrivers;
    private readonly Random _random;

    public MockDriverOfferAcceptorService(
        IServiceScopeFactory scopeFactory,
        ILogger<MockDriverOfferAcceptorService> logger,
        IOptionsMonitor<SimulatorOptions> simulatorOptionsMonitor)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _simulatorOptionsMonitor = simulatorOptionsMonitor;
        _mockDrivers = MockDriverConfiguration.GetMockDrivers();
        _random = new Random();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MockDriverOfferAcceptorService started with {DriverCount} mock drivers", _mockDrivers.Count);

        // Initialize mock drivers in Redis
        await InitializeMockDriversInRedisAsync(stoppingToken);
        var lastTtlRefresh = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var options = _simulatorOptionsMonitor.CurrentValue;
                if (DateTimeOffset.UtcNow - lastTtlRefresh >= TimeSpan.FromSeconds(options.MockDriverTtlRefreshSeconds))
                {
                    await InitializeMockDriversInRedisAsync(stoppingToken);
                    lastTtlRefresh = DateTimeOffset.UtcNow;
                }

                await ProcessOffersAsync(stoppingToken);
                await ProcessConfirmedTripsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing mock driver simulation cycle");
            }

            // Check for offers every 2 seconds
            await Task.Delay(2000, stoppingToken);
        }

        _logger.LogInformation("MockDriverOfferAcceptorService stopped");
    }

    private async Task InitializeMockDriversInRedisAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var redisService = scope.ServiceProvider.GetRequiredService<IRedisService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var mockDriver in _mockDrivers)
        {
            if (!mockDriver.IsActive)
                continue;

            try
            {
                var hasActiveTrip = await dbContext.Trips
                    .AsNoTracking()
                    .AnyAsync(
                        trip => trip.DriverId == mockDriver.DriverId
                            && (trip.TripStatus == TripStatus.ACCEPTED
                                || trip.TripStatus == TripStatus.DRIVER_ARRIVING
                                || trip.TripStatus == TripStatus.ARRIVED
                                || trip.TripStatus == TripStatus.IN_PROGRESS),
                        cancellationToken);
                var nextStatus = hasActiveTrip
                    ? DriverWorkStatus.Busy.ToString()
                    : DriverWorkStatus.Online.ToString();

                var driverProfile = await dbContext.DriverProfiles
                    .FirstOrDefaultAsync(
                        profile => profile.DriverId == mockDriver.DriverId,
                        cancellationToken);
                if (driverProfile is not null)
                {
                    var nextWorkStatus = hasActiveTrip
                        ? DriverWorkStatus.Busy
                        : DriverWorkStatus.Online;
                    if (driverProfile.WorkStatus != nextWorkStatus)
                    {
                        driverProfile.WorkStatus = nextWorkStatus;
                        driverProfile.LastActiveAt = DateTime.UtcNow;
                        driverProfile.UpdatedAt = DateTime.UtcNow;
                    }
                }

                await redisService.SetAsync(RedisKeys.DriverOnline(mockDriver.DriverId), "1", TimeSpan.FromMinutes(30));
                await redisService.SetAsync(RedisKeys.DriverStatus(mockDriver.DriverId), nextStatus, TimeSpan.FromMinutes(30));

                // Add driver location to geo-index
                await redisService.GeoAddAsync(RedisKeys.OnlineDriversGeo, mockDriver.CurrentLng, mockDriver.CurrentLat, mockDriver.DriverId.ToString());

                _logger.LogDebug("Mock driver {DriverId} ({Name}) TTL refreshed as {Status} at ({Lat:F6}, {Lng:F6})",
                    mockDriver.DriverId, mockDriver.Name, nextStatus, mockDriver.CurrentLat, mockDriver.CurrentLng);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize mock driver {DriverId}", mockDriver.DriverId);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessOffersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bookingAssignmentService = scope.ServiceProvider.GetRequiredService<IBookingAssignmentService>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        foreach (var mockDriver in _mockDrivers.Where(d => d.IsActive))
        {
            // Get pending offers for this mock driver (Status: Sent)
            var pendingOffers = await dbContext.BookingDriverOffers
                .Where(o => o.DriverId == mockDriver.DriverId
                    && o.OfferStatus == DriverOfferStatus.Sent
                    && o.ExpiresAt > dateTimeProvider.UtcNow
                    && !mockDriver.ProcessedOffers.Contains(o.Id))
                .ToListAsync(cancellationToken);

            foreach (var offer in pendingOffers)
            {
                mockDriver.ProcessedOffers.Add(offer.Id);

                // Clean up old processed offers
                if (mockDriver.ProcessedOffers.Count > 100)
                {
                    var toRemove = mockDriver.ProcessedOffers.Take(mockDriver.ProcessedOffers.Count - 100).ToList();
                    foreach (var id in toRemove) mockDriver.ProcessedOffers.Remove(id);
                }

                // Decide whether to accept based on acceptance rate
                bool shouldAccept = _random.Next(0, 100) < mockDriver.AcceptanceRatePercent;
                if (!shouldAccept)
                {
                    _logger.LogInformation("Mock driver {DriverId} ({Name}) rejected offer {OfferId}", mockDriver.DriverId, mockDriver.Name, offer.Id);
                    continue;
                }

                // Simulate response delay
                await Task.Delay(mockDriver.ResponseDelaySeconds * 1000, cancellationToken);

                if (_simulatorOptionsMonitor.CurrentValue.MockDriverAutoAcceptOffers)
                {
                    try
                    {
                        // Call AcceptDriverOfferAsync (This sets status to DriverAccepted, doesn't start trip yet)
                        await bookingAssignmentService.AcceptDriverOfferAsync(mockDriver.DriverId, offer.Id, cancellationToken);
                        _logger.LogInformation("Mock driver {DriverId} ({Name}) accepted offer {OfferId}. Waiting for customer confirmation.",
                            mockDriver.DriverId, mockDriver.Name, offer.Id);
                    }
                    catch (Exception ex)
                    {
                        mockDriver.ProcessedOffers.Remove(offer.Id);
                        _logger.LogError(ex, "Mock driver {DriverId} ({Name}) failed to accept offer {OfferId}", mockDriver.DriverId, mockDriver.Name, offer.Id);
                    }
                }
            }
        }
    }

    private async Task ProcessConfirmedTripsAsync(CancellationToken cancellationToken)
    {
        if (!_simulatorOptionsMonitor.CurrentValue.MockDriverAutoProgressAfterConfirm)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        foreach (var mockDriver in _mockDrivers.Where(d => d.IsActive))
        {
            // Find trips where customer confirmed the offer
            var confirmedTrips = await (
                from offer in dbContext.BookingDriverOffers.AsNoTracking()
                join trip in dbContext.Trips.AsNoTracking() on offer.BookingId equals trip.BookingId
                where offer.DriverId == mockDriver.DriverId
                    && offer.OfferStatus == DriverOfferStatus.CustomerConfirmed
                    && trip.DriverId == mockDriver.DriverId
                    && trip.TripStatus == TripStatus.ACCEPTED
                select new { trip.Id, trip.BookingId }
            ).ToListAsync(cancellationToken);

            foreach (var trip in confirmedTrips)
            {
                if (!mockDriver.StartedTrips.Add(trip.Id)) continue;

                _logger.LogInformation("Starting movement for mock driver {DriverId} ({Name}) for confirmed trip {TripId}",
                    mockDriver.DriverId, mockDriver.Name, trip.Id);

                // Start movement simulation in background
                _ = SimulateDriverMovementAsync(mockDriver, trip.BookingId, cancellationToken);
            }
        }
    }

    private async Task SimulateDriverMovementAsync(MockDriver mockDriver, long bookingId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var redisService = scope.ServiceProvider.GetRequiredService<IRedisService>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var realtimeService = scope.ServiceProvider.GetRequiredService<IRealtimeNotificationService>();
        var tripStatusService = scope.ServiceProvider.GetRequiredService<ITripStatusService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<MockDriverOfferAcceptorService>>();

        try
        {
            var autoCompleteTrips = _simulatorOptionsMonitor.CurrentValue.MockDriverAutoCompleteTrips;
            var trip = await dbContext.Trips
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.BookingId == bookingId && t.DriverId == mockDriver.DriverId, cancellationToken);
            if (trip is null || trip.TripStatus == TripStatus.CANCELLED) return;

            var booking = await dbContext.Bookings
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);
            if (booking is null || booking.BookingStatus == BookingStatus.Cancelled) return;

            // 1. DRIVER_ARRIVING: Current -> Pickup
            await tripStatusService.UpdateDriverTripStatusAsync(
                mockDriver.DriverId,
                trip.Id,
                TripStatus.DRIVER_ARRIVING,
                cancellationToken);
            trip = await dbContext.Trips.AsNoTracking().FirstAsync(t => t.Id == trip.Id, cancellationToken);

            var mapRoutingService = scope.ServiceProvider.GetRequiredService<IMapRoutingService>();
            List<(double Lat, double Lng)> arrivalPath;
            try
            {
                var routeEstimate = await mapRoutingService.GetRouteEstimateAsync(
                    new RouteEstimateRequest
                    {
                        Origin = new LocationPoint(mockDriver.CurrentLat, mockDriver.CurrentLng),
                        Destination = new LocationPoint(booking.PickupLocation.Y, booking.PickupLocation.X),
                        Provider = MapProvider.Auto,
                        TravelMode = MapTravelMode.Car,
                        IncludePolyline = true,
                        RequestSource = "DriverMatching"
                    },
                    cancellationToken);

                if (!string.IsNullOrEmpty(routeEstimate?.EncodedPolyline))
                {
                    var decodedPath = PolylineUtils.Decode(routeEstimate.EncodedPolyline);
                    // OpenRouteService geometry polyline might be encoded as [lng, lat]
                    // If Lat > 90, we know it's swapped (for Vietnam, Lat is ~16, Lng is ~108)
                    if (decodedPath.Count > 0 && Math.Abs(decodedPath[0].Lat) > 90)
                    {
                        decodedPath = decodedPath.Select(p => (p.Lng, p.Lat)).ToList();
                    }
                    arrivalPath = decodedPath;
                }
                else
                {
                    arrivalPath = new List<(double Lat, double Lng)> { (mockDriver.CurrentLat, mockDriver.CurrentLng), (booking.PickupLocation.Y, booking.PickupLocation.X) };
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to get route estimate for driver arrival path. Falling back to direct line.");
                arrivalPath = new List<(double Lat, double Lng)> { (mockDriver.CurrentLat, mockDriver.CurrentLng), (booking.PickupLocation.Y, booking.PickupLocation.X) };
            }

            bool arrived = await MoveDriverAlongPathAsync(mockDriver, arrivalPath, 13.8, booking, trip, realtimeService, redisService, dateTimeProvider, logger, cancellationToken);
            if (!arrived) return;

            // Re-fetch to check for cancellation
            trip = await dbContext.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == trip.Id, cancellationToken);
            if (trip is null || trip.TripStatus == TripStatus.CANCELLED) return;

            // 2. ARRIVED: At pickup
            await tripStatusService.UpdateDriverTripStatusAsync(
                mockDriver.DriverId,
                trip.Id,
                TripStatus.ARRIVED,
                cancellationToken);
            await Task.Delay(5000, cancellationToken);

            // Re-fetch to check for cancellation
            trip = await dbContext.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == trip.Id, cancellationToken);
            if (trip is null || trip.TripStatus == TripStatus.CANCELLED) return;

            // 3. IN_PROGRESS: Pickup -> Destination
            await tripStatusService.UpdateDriverTripStatusAsync(
                mockDriver.DriverId,
                trip.Id,
                TripStatus.IN_PROGRESS,
                cancellationToken);
            trip = await dbContext.Trips.AsNoTracking().FirstAsync(t => t.Id == trip.Id, cancellationToken);
            if (!autoCompleteTrips)
            {
                logger.LogInformation("Trip {TripId} left IN_PROGRESS for manual completion by customer or driver", trip.Id);
                return;
            }

            bool completed;
            if (string.IsNullOrEmpty(booking.RoutePolyline))
            {
                var directPath = new List<(double Lat, double Lng)> { (booking.PickupLocation.Y, booking.PickupLocation.X), (booking.DestinationLocation!.Y, booking.DestinationLocation.X) };
                completed = await MoveDriverAlongPathAsync(mockDriver, directPath, 11.1, booking, trip, realtimeService, redisService, dateTimeProvider, logger, cancellationToken);
            }
            else
            {
                var decodedPath = PolylineUtils.Decode(booking.RoutePolyline);
                if (decodedPath.Count > 0 && Math.Abs(decodedPath[0].Lat) > 90)
                {
                    decodedPath = decodedPath.Select(p => (p.Lng, p.Lat)).ToList();
                }
                completed = await MoveDriverAlongPathAsync(mockDriver, decodedPath, 11.1, booking, trip, realtimeService, redisService, dateTimeProvider, logger, cancellationToken);
            }

            if (!completed) return;

            // Re-fetch to check for cancellation
            trip = await dbContext.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == trip.Id, cancellationToken);
            if (trip is null || trip.TripStatus == TripStatus.CANCELLED) return;

            // 4. COMPLETED: Done
            await tripStatusService.CompleteTripAsync(
                mockDriver.DriverId,
                trip.Id,
                cancellationToken);

            logger.LogInformation("Trip {TripId} completed for mock driver {DriverId}", trip.Id, mockDriver.DriverId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error simulating movement for mock driver {DriverId}", mockDriver.DriverId);
        }
    }

    private async Task<bool> MoveDriverAlongPathAsync(MockDriver mockDriver, List<(double Lat, double Lng)> path, double speedMs, Booking booking, Trip trip, IRealtimeNotificationService realtimeService, IRedisService redisService, IDateTimeProvider dateTimeProvider, ILogger logger, CancellationToken ct)
    {
        double totalDistance = PolylineUtils.CalculateTotalDistance(path);
        double currentDistance = 0;
        const int intervalMs = 1000;
        int checkCounter = 0;

        while (currentDistance < totalDistance)
        {
            if (ct.IsCancellationRequested) return false;

            // Every 5 seconds, check database for trip cancellation
            if (checkCounter % 5 == 0)
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var currentTrip = await dbContext.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == trip.Id, ct);
                if (currentTrip is null ||
                    currentTrip.TripStatus is TripStatus.CANCELLED or TripStatus.COMPLETED)
                {
                    logger.LogInformation("Movement stopped for mock driver {DriverId} because trip {TripId} is {TripStatus}", mockDriver.DriverId, trip.Id, currentTrip?.TripStatus);
                    return false;
                }
            }

            var point = PolylineUtils.GetPointAtDistance(path, currentDistance);
            mockDriver.CurrentLat = point.Lat;
            mockDriver.CurrentLng = point.Lng;

            await redisService.SetAsync(RedisKeys.DriverOnline(mockDriver.DriverId), "1", TimeSpan.FromMinutes(5));
            await redisService.SetAsync(RedisKeys.DriverStatus(mockDriver.DriverId), "Busy", TimeSpan.FromMinutes(5));
            await redisService.GeoAddAsync(RedisKeys.OnlineDriversGeo, point.Lng, point.Lat, mockDriver.DriverId.ToString());

            await realtimeService.PublishDriverLocationUpdatedAsync(new DriverLocationUpdatedEvent(mockDriver.DriverId, booking.CustomerId, trip.Id, point.Lat, point.Lng, dateTimeProvider.UtcNow), ct);

            await Task.Delay(intervalMs, ct);
            currentDistance += speedMs * (intervalMs / 1000.0);
            checkCounter++;
        }

        mockDriver.CurrentLat = path[^1].Lat;
        mockDriver.CurrentLng = path[^1].Lng;
        return true;
    }
}
