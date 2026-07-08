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
using System.Text.Json;

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

        try
        {
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
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
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
                    }
                    driverProfile.LastActiveAt = DateTime.UtcNow;
                    driverProfile.UpdatedAt = DateTime.UtcNow;
                }

                await redisService.SetAsync(RedisKeys.DriverOnline(mockDriver.DriverId), "1", TimeSpan.FromMinutes(30));
                await redisService.SetAsync(RedisKeys.DriverStatus(mockDriver.DriverId), nextStatus, TimeSpan.FromMinutes(30));

                // Add driver location to geo-index
                await redisService.GeoAddAsync(RedisKeys.OnlineDriversGeo, mockDriver.CurrentLng, mockDriver.CurrentLat, mockDriver.DriverId.ToString());

                var location = new DriverLocationCache(mockDriver.DriverId, mockDriver.CurrentLat, mockDriver.CurrentLng, DateTime.UtcNow);
                await redisService.SetAsync(RedisKeys.DriverLocation(mockDriver.DriverId), JsonSerializer.Serialize(location), TimeSpan.FromMinutes(30));

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
        var redisService = scope.ServiceProvider.GetRequiredService<IRedisService>();

        foreach (var mockDriver in _mockDrivers.Where(d => d.IsActive))
        {
            // Get pending offers for this mock driver (Status: Sent)
            var pendingOffers = await dbContext.BookingDriverOffers
                .Include(o => o.Booking)
                .Where(o => o.DriverId == mockDriver.DriverId
                    && o.OfferStatus == DriverOfferStatus.Sent
                    && o.ExpiresAt > dateTimeProvider.UtcNow
                    && o.Booking.BookingStatus == BookingStatus.Searching
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

                // Check if driver is already busy
                var driverProfile = await dbContext.DriverProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.DriverId == mockDriver.DriverId, cancellationToken);
                var activeTrips = await dbContext.Trips.AsNoTracking()
                    .Where(t => t.DriverId == mockDriver.DriverId &&
                                (t.TripStatus == TripStatus.ACCEPTED || t.TripStatus == TripStatus.DRIVER_ARRIVING || t.TripStatus == TripStatus.ARRIVED || t.TripStatus == TripStatus.IN_PROGRESS))
                    .ToListAsync(cancellationToken);
                
                bool isBusy = (driverProfile?.WorkStatus == DriverWorkStatus.Busy) || activeTrips.Any();

                if (isBusy)
                {
                    SimulatorConsoleOutput.Print("[SIM]", "[DRIVER_ACCEPT][SKIP_BUSY]", new {
                        driverId = mockDriver.DriverId,
                        offerId = offer.Id,
                        bookingId = offer.BookingId,
                        workStatus = driverProfile?.WorkStatus.ToString(),
                        activeTripIds = activeTrips.Select(t => t.Id).ToList()
                    }, _simulatorOptionsMonitor.CurrentValue.EnableSimulatorConsoleOutput);

                    try
                    {
                        await bookingAssignmentService.RejectDriverOfferAsync(mockDriver.DriverId, offer.Id, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to auto-reject offer {OfferId} for busy mock driver", offer.Id);
                    }
                    continue;
                }

                // Decide whether to accept based on acceptance rate
                bool shouldAccept = _random.Next(0, 100) < mockDriver.AcceptanceRatePercent;
                if (!shouldAccept)
                {
                    _logger.LogInformation("Mock driver {DriverId} ({Name}) rejected offer {OfferId}", mockDriver.DriverId, mockDriver.Name, offer.Id);
                    try
                    {
                        await bookingAssignmentService.RejectDriverOfferAsync(mockDriver.DriverId, offer.Id, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to auto-reject offer {OfferId} for mock driver due to acceptance rate", offer.Id);
                    }
                    continue;
                }

                // Simulate response delay
                await Task.Delay(mockDriver.ResponseDelaySeconds * 1000, cancellationToken);

                if (_simulatorOptionsMonitor.CurrentValue.MockDriverAutoAcceptOffers)
                {
                    try
                    {
                        // Print state before accept
                        await PrintDriverStateAsync("BEFORE_ACCEPT", mockDriver.DriverId, redisService, dbContext, _simulatorOptionsMonitor.CurrentValue.EnableSimulatorConsoleOutput, cancellationToken);

                        // Re-check conditions before accepting to avoid business rule errors
                        var freshOffer = await dbContext.BookingDriverOffers.Include(o => o.Booking).AsNoTracking().FirstOrDefaultAsync(o => o.Id == offer.Id, cancellationToken);
                        if (freshOffer == null || freshOffer.OfferStatus != DriverOfferStatus.Sent || freshOffer.ExpiresAt <= dateTimeProvider.UtcNow || freshOffer.Booking.BookingStatus != BookingStatus.Searching)
                        {
                            _logger.LogInformation("Skipping accept because offer {OfferId} is no longer valid for accept.", offer.Id);
                            continue;
                        }

                        // Call AcceptDriverOfferAsync (This sets status to DriverAccepted, doesn't start trip yet)
                        await bookingAssignmentService.AcceptDriverOfferAsync(mockDriver.DriverId, offer.Id, cancellationToken);
                        
                        SimulatorConsoleOutput.Print("[SIM]", "[DRIVER_ACCEPT][SUCCESS]", new {
                            driverId = mockDriver.DriverId,
                            driverName = mockDriver.Name,
                            offerId = offer.Id,
                            bookingId = offer.BookingId
                        }, _simulatorOptionsMonitor.CurrentValue.EnableSimulatorConsoleOutput);

                        // Print state after accept
                        await PrintDriverStateAsync("AFTER_ACCEPT", mockDriver.DriverId, redisService, dbContext, _simulatorOptionsMonitor.CurrentValue.EnableSimulatorConsoleOutput, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Mock driver {DriverId} ({Name}) failed to accept offer {OfferId}", mockDriver.DriverId, mockDriver.Name, offer.Id);
                    }
                }
            }
        }
    }

    private async Task PrintDriverStateAsync(string actionContext, Guid driverId, IRedisService redisService, ApplicationDbContext dbContext, bool enabled, CancellationToken ct)
    {
        if (!enabled) return;

        var profile = await dbContext.DriverProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.DriverId == driverId, ct);
        var activeTrip = await dbContext.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.DriverId == driverId && (t.TripStatus == TripStatus.ACCEPTED || t.TripStatus == TripStatus.DRIVER_ARRIVING || t.TripStatus == TripStatus.ARRIVED || t.TripStatus == TripStatus.IN_PROGRESS), ct);
        
        var onlineStr = await redisService.GetAsync(RedisKeys.DriverOnline(driverId));
        var statusStr = await redisService.GetAsync(RedisKeys.DriverStatus(driverId));
        var locStr = await redisService.GetAsync(RedisKeys.DriverLocation(driverId));

        SimulatorConsoleOutput.Print("[SIM]", $"[STATE][{actionContext}]", new {
            driverId = driverId,
            workStatus = profile?.WorkStatus.ToString(),
            lastActiveAt = profile?.LastActiveAt,
            activeTripId = activeTrip?.Id,
            activeTripBookingId = activeTrip?.BookingId,
            activeTripStatus = activeTrip?.TripStatus.ToString(),
            redisOnline = onlineStr,
            redisStatus = statusStr,
            redisLocation = locStr
        }, enabled);
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
            var activeTrips = await dbContext.Trips.AsNoTracking()
                .Where(trip => trip.DriverId == mockDriver.DriverId
                    && (trip.TripStatus == TripStatus.ACCEPTED 
                        || trip.TripStatus == TripStatus.DRIVER_ARRIVING
                        || trip.TripStatus == TripStatus.IN_PROGRESS))
                .Select(trip => new { trip.Id, trip.BookingId })
                .ToListAsync(cancellationToken);

            foreach (var trip in activeTrips)
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
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

            var autoCompleteTrips = _simulatorOptionsMonitor.CurrentValue.MockDriverAutoCompleteTrips;
            var trip = await dbContext.Trips
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.BookingId == bookingId && t.DriverId == mockDriver.DriverId, cancellationToken);
            if (trip is null || trip.TripStatus == TripStatus.CANCELLED) return;

            var booking = await dbContext.Bookings
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);
            if (booking is null || booking.BookingStatus == BookingStatus.Cancelled) return;

            if (trip.TripStatus == TripStatus.ACCEPTED || trip.TripStatus == TripStatus.DRIVER_ARRIVING)
            {
                // 1. DRIVER_ARRIVING: Current -> Pickup
                if (trip.TripStatus == TripStatus.ACCEPTED)
                {
                    await tripStatusService.UpdateDriverTripStatusAsync(
                        mockDriver.DriverId,
                        trip.Id,
                        TripStatus.DRIVER_ARRIVING,
                        cancellationToken);
                    trip = await dbContext.Trips.AsNoTracking().FirstAsync(t => t.Id == trip.Id, cancellationToken);
                }

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
            // chỉnh tốc độ di chuyển của tài xế 13.8 m/s ~ 50 km/h có thể thay đổi tùy ý
                bool arrived = await MoveDriverAlongPathAsync(mockDriver, arrivalPath, 13.8, booking, trip, realtimeService, redisService, dateTimeProvider, logger, cancellationToken);
                if (!arrived) return;
            }

            // Re-fetch to check for cancellation
            trip = await dbContext.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == trip.Id, cancellationToken);
            if (trip is null || trip.TripStatus == TripStatus.CANCELLED) return;

            if (trip.TripStatus == TripStatus.DRIVER_ARRIVING || trip.TripStatus == TripStatus.ARRIVED)
            {
                // 2. ARRIVED: At pickup
                if (trip.TripStatus == TripStatus.DRIVER_ARRIVING)
                {
                    await tripStatusService.UpdateDriverTripStatusAsync(
                        mockDriver.DriverId,
                        trip.Id,
                        TripStatus.ARRIVED,
                        cancellationToken);
                }
                await Task.Delay(5000, cancellationToken);
            }

            // Re-fetch to check for cancellation
            trip = await dbContext.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == trip.Id, cancellationToken);
            if (trip is null || trip.TripStatus == TripStatus.CANCELLED) return;

            if (trip.TripStatus == TripStatus.ARRIVED || trip.TripStatus == TripStatus.IN_PROGRESS)
            {
                // 3. IN_PROGRESS: Pickup -> Destination
                if (trip.TripStatus == TripStatus.ARRIVED)
                {
                    await tripStatusService.UpdateDriverTripStatusAsync(
                        mockDriver.DriverId,
                        trip.Id,
                        TripStatus.IN_PROGRESS,
                        cancellationToken);
                    trip = await dbContext.Trips.AsNoTracking().FirstAsync(t => t.Id == trip.Id, cancellationToken);
                }
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
            }

            // Re-fetch to check for cancellation
            trip = await dbContext.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == trip.Id, cancellationToken);
            if (trip is null || trip.TripStatus == TripStatus.CANCELLED) return;

            // 4. WAITING_RETURN_CONFIRM: Driver ends trip
            await tripStatusService.EndTripAsync(
                mockDriver.DriverId,
                trip.Id,
                cancellationToken);

            logger.LogInformation("Trip {TripId} reached WAITING_RETURN_CONFIRM for mock driver {DriverId}", trip.Id, mockDriver.DriverId);

            // Wait for customer to confirm return on TripSummaryPage. The customer
            // confirmation advances RETURN_CONFIRMED to WAITING_PAYMENT.
            int waitCounter = 0;
            while (true)
            {
                if (cancellationToken.IsCancellationRequested) return;
                await Task.Delay(2000, cancellationToken);
                waitCounter++;

                trip = await dbContext.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == trip.Id, cancellationToken);
                if (trip is null || trip.TripStatus == TripStatus.CANCELLED) return;

                if (trip.TripStatus == TripStatus.WAITING_PAYMENT)
                {
                    break;
                }

                if (waitCounter > 150) // Wait 5 minutes
                {
                    logger.LogWarning("Mock driver {DriverId} gave up waiting for customer to confirm return for trip {TripId}", mockDriver.DriverId, trip.Id);
                    return;
                }
            }

            // 5. Mock Payment: Auto-confirm cash payment after a short delay.
            // Payment success is responsible for moving WAITING_PAYMENT to COMPLETED.
            try
            {
                await Task.Delay(5000, cancellationToken); // Wait 5s before confirming payment
                var paymentService = scope.ServiceProvider.GetRequiredService<SafeRide.Application.Common.Interfaces.IPaymentService>();
                await paymentService.ConfirmCashPaymentAsync(mockDriver.DriverId, trip.Id, cancellationToken);
                logger.LogInformation("Payment confirmed via cash and trip completed for mock driver {DriverId} trip {TripId}", mockDriver.DriverId, trip.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to auto-confirm cash payment for mock driver {DriverId} trip {TripId}", mockDriver.DriverId, trip.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error simulating movement for mock driver {DriverId}", mockDriver.DriverId);
        }
    }

    private async Task<bool> MoveDriverAlongPathAsync(MockDriver mockDriver, List<(double Lat, double Lng)> path, double speedMs, Booking booking, Trip trip, IRealtimeNotificationService realtimeService, IRedisService redisService, IDateTimeProvider dateTimeProvider, ILogger logger, CancellationToken ct)
    {
        if (path == null || path.Count < 2) return false;

        double totalDistance = PolylineUtils.CalculateTotalDistance(path);
        double currentDistance = 0;
        
        // Resume logic: Use driver's current coordinates to find where they left off
        var locStr = await redisService.GetAsync(RedisKeys.DriverLocation(mockDriver.DriverId));
        if (!string.IsNullOrEmpty(locStr))
        {
            var parts = locStr.Split(',');
            if (parts.Length == 2 && double.TryParse(parts[0], out var lat) && double.TryParse(parts[1], out var lng))
            {
                // Only resume if they actually moved significantly, to avoid getting stuck at start
                var startDist = PolylineUtils.GetDistance(path[0].Lat, path[0].Lng, lat, lng);
                if (startDist > 5.0)
                {
                    currentDistance = PolylineUtils.GetDistanceAlongPathToClosestPoint(path, lat, lng);
                    mockDriver.CurrentLat = lat;
                    mockDriver.CurrentLng = lng;
                }
            }
        }

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
            
            var locationCache = new DriverLocationCache(mockDriver.DriverId, point.Lat, point.Lng, dateTimeProvider.UtcNow);
            await redisService.SetAsync(RedisKeys.DriverLocation(mockDriver.DriverId), JsonSerializer.Serialize(locationCache), TimeSpan.FromMinutes(5));

            await realtimeService.PublishDriverLocationUpdatedAsync(new DriverLocationUpdatedEvent(mockDriver.DriverId, booking.CustomerId, trip.Id, point.Lat, point.Lng, dateTimeProvider.UtcNow), ct);

            if (checkCounter % 5 == 0)
            {
                SimulatorConsoleOutput.Print("[SIM]", "[LOCATION]", new {
                    driverId = mockDriver.DriverId,
                    tripId = trip.Id,
                    lat = point.Lat,
                    lng = point.Lng
                }, _simulatorOptionsMonitor.CurrentValue.EnableSimulatorConsoleOutput);
            }

            var skipDelay = _simulatorOptionsMonitor.CurrentValue.MockDriverSkipMovementDelay;
            if (!skipDelay)
            {
                await Task.Delay(intervalMs, ct);
            }
            
            currentDistance += speedMs * (skipDelay ? 10 : (intervalMs / 1000.0)); // Move 10x faster if skipping delay
            checkCounter++;
        }

        mockDriver.CurrentLat = path[^1].Lat;
        mockDriver.CurrentLng = path[^1].Lng;
        
        // Publish final point
        await redisService.GeoAddAsync(RedisKeys.OnlineDriversGeo, mockDriver.CurrentLng, mockDriver.CurrentLat, mockDriver.DriverId.ToString());
        var finalLocationCache = new DriverLocationCache(mockDriver.DriverId, mockDriver.CurrentLat, mockDriver.CurrentLng, dateTimeProvider.UtcNow);
        await redisService.SetAsync(RedisKeys.DriverLocation(mockDriver.DriverId), JsonSerializer.Serialize(finalLocationCache), TimeSpan.FromMinutes(5));
        await realtimeService.PublishDriverLocationUpdatedAsync(new DriverLocationUpdatedEvent(mockDriver.DriverId, booking.CustomerId, trip.Id, mockDriver.CurrentLat, mockDriver.CurrentLng, dateTimeProvider.UtcNow), ct);

        SimulatorConsoleOutput.Print("[SIM]", "[LOCATION][FINAL]", new {
            driverId = mockDriver.DriverId,
            tripId = trip.Id,
            lat = mockDriver.CurrentLat,
            lng = mockDriver.CurrentLng
        }, _simulatorOptionsMonitor.CurrentValue.EnableSimulatorConsoleOutput);

        return true;
    }
}
