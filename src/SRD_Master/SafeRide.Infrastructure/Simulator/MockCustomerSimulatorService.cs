using System.Text.Json;
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
using SafeRide.Infrastructure.ExternalServices.VietMap;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.Infrastructure.Simulator;

/// <summary>
/// Background service that automatically confirms driver offers on behalf of customers.
/// It also acts as an auto-flow trigger for Real Drivers to help demo UI routing flows.
/// To test the Real Driver UI location flow manually, set RealDriverAutoProgressTrips = false 
/// and RealDriverSimulateMovement = false in SimulatorOptions.
/// </summary>
public sealed class MockCustomerSimulatorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MockCustomerSimulatorService> _logger;
    private readonly IOptionsMonitor<SimulatorOptions> _simulatorOptionsMonitor;
    private readonly SafeRide.Infrastructure.Redis.IRedisService _redisService;
 

    // Use a hash set to track trips we are currently simulating so we don't simulate multiple times
    private readonly HashSet<long> _simulatingTrips = new();

    public MockCustomerSimulatorService(
        IServiceScopeFactory scopeFactory,
        ILogger<MockCustomerSimulatorService> logger,
        IOptionsMonitor<SimulatorOptions> simulatorOptionsMonitor,
        SafeRide.Infrastructure.Redis.IRedisService redisService)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _simulatorOptionsMonitor = simulatorOptionsMonitor;
        _redisService = redisService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MockCustomerSimulatorService started");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var options = _simulatorOptionsMonitor.CurrentValue;

                    // 1. Auto confirm customer offers
                    if (options.MockCustomerAutoConfirmDriver)
                    {
                        await ProcessCustomerConfirmationsAsync(stoppingToken);
                    }

                    // 2. Auto accept offers for real drivers
                    if (options.RealDriverAutoAcceptOffers)
                    {
                        await ProcessRealDriverOfferAcceptanceAsync(stoppingToken);
                    }

                    // 3. Auto progress trips and movement for real drivers
                    if (options.RealDriverAutoProgressTrips)
                    {
                        await ProcessRealDriverTripsAsync(stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing mock customer/demo simulation cycle");
                }

                await Task.Delay(2000, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }

        _logger.LogInformation("MockCustomerSimulatorService stopped");
    }

    private async Task ProcessCustomerConfirmationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bookingAssignmentService = scope.ServiceProvider.GetRequiredService<IBookingAssignmentService>();

        var acceptedOffers = await dbContext.BookingDriverOffers
            .Include(o => o.Booking)
            .Where(o => o.OfferStatus == DriverOfferStatus.DriverAccepted)
            .ToListAsync(cancellationToken);

        foreach (var offer in acceptedOffers)
        {
            try
            {
                await Task.Delay(1000, cancellationToken);
                if (!_simulatorOptionsMonitor.CurrentValue.MockCustomerAutoConfirmDriver) break;

                _logger.LogInformation("MockCustomerSimulatorService is auto-confirming offer {OfferId} for booking {BookingId}", offer.Id, offer.BookingId);
                await bookingAssignmentService.ConfirmDriverAsync(offer.Booking.CustomerId, offer.BookingId, offer.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MockCustomerSimulatorService failed to auto-confirm offer {OfferId}", offer.Id);
            }
        }
    }

    private async Task ProcessRealDriverOfferAcceptanceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bookingAssignmentService = scope.ServiceProvider.GetRequiredService<IBookingAssignmentService>();
        var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        // Find sent offers to any driver
        var sentOffers = await dbContext.BookingDriverOffers
            .Where(o => o.OfferStatus == DriverOfferStatus.Sent && o.ExpiresAt > dateTimeProvider.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var offer in sentOffers)
        {
            // Skip mock drivers since MockDriverOfferAcceptorService handles them
            var isMockDriver = MockDriverConfiguration.GetMockDrivers().Any(md => md.DriverId == offer.DriverId);
            if (isMockDriver) continue;

            try
            {
                await Task.Delay(1500, cancellationToken);
                if (!_simulatorOptionsMonitor.CurrentValue.RealDriverAutoAcceptOffers) break;

                _logger.LogInformation("DemoFlow is auto-accepting offer {OfferId} for real driver {DriverId}", offer.Id, offer.DriverId);
                await bookingAssignmentService.AcceptDriverOfferAsync(offer.DriverId, offer.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DemoFlow failed to auto-accept offer {OfferId}", offer.Id);
            }
        }
    }

    private async Task ProcessRealDriverTripsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var activeTrips = await dbContext.Trips
            .Include(t => t.Booking)
            .Where(t => t.TripStatus == TripStatus.ACCEPTED || t.TripStatus == TripStatus.ARRIVED || t.TripStatus == TripStatus.IN_PROGRESS)
            .ToListAsync(cancellationToken);

        foreach (var trip in activeTrips)
        {
            var isMockDriver = MockDriverConfiguration.GetMockDrivers().Any(md => md.DriverId == trip.DriverId);
            if (isMockDriver) continue;

            lock (_simulatingTrips)
            {
                if (_simulatingTrips.Contains(trip.Id)) continue;
                _simulatingTrips.Add(trip.Id);
            }

            _logger.LogInformation("DemoFlow starting movement simulation for real driver {DriverId} on trip {TripId}", trip.DriverId, trip.Id);
            _ = SimulateRealDriverMovementAsync(trip.Id, cancellationToken);
        }
    }

    private async Task SimulateRealDriverMovementAsync(long tripId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var redisService = scope.ServiceProvider.GetRequiredService<IRedisService>();
            var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
            var realtimeService = scope.ServiceProvider.GetRequiredService<IRealtimeNotificationService>();
            var tripStatusService = scope.ServiceProvider.GetRequiredService<ITripStatusService>();
            var mapRoutingService = scope.ServiceProvider.GetRequiredService<IMapRoutingService>();

            var trip = await dbContext.Trips.Include(t => t.Booking).FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);
            if (trip is null || trip.TripStatus == TripStatus.CANCELLED || trip.TripStatus == TripStatus.COMPLETED) return;

            var driverId = trip.DriverId;
            var booking = trip.Booking;

            // Determine driver's starting location
            double startLat = booking.PickupLocation.Y + 0.01; // offset by default
            double startLng = booking.PickupLocation.X + 0.01;
            
            var cachedLocStr = await redisService.GetAsync(RedisKeys.DriverLocation(driverId));
            if (!string.IsNullOrEmpty(cachedLocStr))
            {
                var cachedLoc = JsonSerializer.Deserialize<DriverLocationCache>(cachedLocStr);
                if (cachedLoc is null || (cachedLoc.Latitude == 0 && cachedLoc.Longitude == 0)) { /* use default */ }
                else
                {
                    startLat = cachedLoc.Latitude;
                    startLng = cachedLoc.Longitude;
                }
            }

            var currentLoc = (Lat: startLat, Lng: startLng);

            var options = _simulatorOptionsMonitor.CurrentValue;

            // If ACCEPTED -> Move to Pickup -> ARRIVED
            if (trip.TripStatus == TripStatus.ACCEPTED)
            {
                await tripStatusService.UpdateDriverTripStatusAsync(driverId, trip.Id, TripStatus.DRIVER_ARRIVING, cancellationToken);
                
                if (options.RealDriverSimulateMovement)
                {
                    List<(double Lat, double Lng)> arrivalPath;
                    try
                    {
                        var routeEstimate = await mapRoutingService.GetRouteEstimateAsync(
                            new RouteEstimateRequest
                            {
                                Origin = new LocationPoint(currentLoc.Lat, currentLoc.Lng),
                                Destination = new LocationPoint(booking.PickupLocation.Y, booking.PickupLocation.X),
                                Provider = MapProvider.Auto,
                                TravelMode = MapTravelMode.Car,
                                IncludePolyline = true,
                                RequestSource = "DriverMatching"
                            }, cancellationToken);

                        if (!string.IsNullOrEmpty(routeEstimate?.EncodedPolyline))
                        {
                            await redisPolyline(routeEstimate.EncodedPolyline);

                            var decodedPath = PolylineUtils.Decode(routeEstimate.EncodedPolyline);
                            if (decodedPath.Count > 0 && Math.Abs(decodedPath[0].Lat) > 90) decodedPath = decodedPath.Select(p => (p.Lng, p.Lat)).ToList();
                            arrivalPath = decodedPath;
                        }
                        else arrivalPath = new List<(double, double)> { currentLoc, (booking.PickupLocation.Y, booking.PickupLocation.X) };
                    }
                    catch
                    {
                        arrivalPath = new List<(double, double)> { currentLoc, (booking.PickupLocation.Y, booking.PickupLocation.X) };
                    }

                    var arrived = await MoveAlongPathAsync(arrivalPath, 13.8, currentLoc, driverId, booking, trip, realtimeService, redisService, dateTimeProvider, cancellationToken);
                    if (!arrived) return;
                    currentLoc = arrivalPath.Last();
                }

                // Check cancel
                trip = await dbContext.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);
                if (trip is null || trip.TripStatus == TripStatus.CANCELLED) return;

                await tripStatusService.UpdateDriverTripStatusAsync(driverId, trip.Id, TripStatus.ARRIVED, cancellationToken);
                await Task.Delay(3000, cancellationToken);
            }

            trip = await dbContext.Trips.Include(t => t.Booking).FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);
            if (trip is null || trip.TripStatus == TripStatus.CANCELLED || trip.TripStatus == TripStatus.COMPLETED) return;

            // If ARRIVED -> Start Trip -> IN_PROGRESS
            if (trip.TripStatus == TripStatus.ARRIVED)
            {
                await tripStatusService.UpdateDriverTripStatusAsync(driverId, trip.Id, TripStatus.IN_PROGRESS, cancellationToken);
                trip.TripStatus = TripStatus.IN_PROGRESS;
            }

            if (trip.TripStatus == TripStatus.IN_PROGRESS)
            {
                if (options.RealDriverSimulateMovement)
                {
                    List<(double Lat, double Lng)> tripPath;
                    if (string.IsNullOrEmpty(booking.RoutePolyline))
                    {
                        tripPath = new List<(double, double)> { currentLoc, (booking.DestinationLocation!.Y, booking.DestinationLocation.X) };
                    }
                    else
                    {
                        var decodedPath = PolylineUtils.Decode(booking.RoutePolyline);
                        if (decodedPath.Count > 0 && Math.Abs(decodedPath[0].Lat) > 90) decodedPath = decodedPath.Select(p => (p.Lng, p.Lat)).ToList();
                        tripPath = decodedPath;
                        
                        // if currentLoc is too far from path start, just prepend it
                        if (tripPath.Any())
                        {
                            tripPath.Insert(0, currentLoc);
                        }
                    }

                    var completed = await MoveAlongPathAsync(tripPath, 11.1, currentLoc, driverId, booking, trip, realtimeService, redisService, dateTimeProvider, cancellationToken);
                    if (!completed) return;
                } 
                else
                {
                    await redisPolyline(booking.RoutePolyline);
                }

                trip = await dbContext.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tripId, cancellationToken);
                if (trip is null || trip.TripStatus == TripStatus.CANCELLED) return;

                await tripStatusService.CompleteTripAsync(driverId, trip.Id, cancellationToken);
                _logger.LogInformation("DemoFlow completed trip {TripId} for real driver {DriverId}", trip.Id, driverId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DemoFlow error simulating movement for trip {TripId}", tripId);
        }
        finally
        {
            lock (_simulatingTrips)
            {
                _simulatingTrips.Remove(tripId);
            }
        }
    }

    private async Task<bool> MoveAlongPathAsync(
        List<(double Lat, double Lng)> path, 
        double speedMs, 
        (double Lat, double Lng) refLoc,
        Guid driverId, 
        Booking booking, 
        Trip trip, 
        IRealtimeNotificationService realtimeService, 
        IRedisService redisService, 
        IDateTimeProvider dateTimeProvider, 
        CancellationToken ct)
    {
        if (path.Count < 2) return true;

        double totalDistance = PolylineUtils.CalculateTotalDistance(path);
        double currentDistance = 0;
        const int intervalMs = 1000;
        int checkCounter = 0;

        while (currentDistance < totalDistance)
        {
            if (ct.IsCancellationRequested) return false;

            if (checkCounter % 5 == 0)
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var currentTrip = await dbContext.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == trip.Id, ct);
                if (currentTrip is null || currentTrip.TripStatus is TripStatus.CANCELLED or TripStatus.COMPLETED)
                {
                    return false;
                }

                // Keep driver alive in SQL so they don't get kicked offline after the trip completes
                var profile = await dbContext.DriverProfiles.FirstOrDefaultAsync(p => p.DriverId == driverId, ct);
                if (profile != null)
                {
                    profile.LastActiveAt = dateTimeProvider.UtcNow;
                    await dbContext.SaveChangesAsync(ct);
                }
            }

            var point = PolylineUtils.GetPointAtDistance(path, currentDistance);

            await redisService.SetAsync(RedisKeys.DriverOnline(driverId), "1", TimeSpan.FromMinutes(5));
            await redisService.SetAsync(RedisKeys.DriverStatus(driverId), "Busy", TimeSpan.FromMinutes(5));
            await redisService.GeoAddAsync(RedisKeys.OnlineDriversGeo, point.Lng, point.Lat, driverId.ToString());
            
            var locationCache = new DriverLocationCache(driverId, point.Lat, point.Lng, dateTimeProvider.UtcNow);
            await redisService.SetAsync(RedisKeys.DriverLocation(driverId), JsonSerializer.Serialize(locationCache), TimeSpan.FromMinutes(5));


            await realtimeService.PublishDriverLocationUpdatedAsync(new DriverLocationUpdatedEvent(driverId, booking.CustomerId, trip.Id, point.Lat, point.Lng, dateTimeProvider.UtcNow), ct);

            var skipDelay = _simulatorOptionsMonitor.CurrentValue.MockDriverSkipMovementDelay;
            if (!skipDelay)
            {
                await Task.Delay(intervalMs, ct);
            }
            
            currentDistance += speedMs * (skipDelay ? 10 : (intervalMs / 1000.0));
            checkCounter++;
        }

        return true;
    }

    public async Task redisPolyline(string? encodedPolyline)
    {
        try
            {
                var debugKey = $"debug:polyline:{Guid.NewGuid()}";
                await _redisService.SetAsync(debugKey, encodedPolyline ?? "", TimeSpan.FromMinutes(15));
                _logger.LogInformation("Saved polyline to Redis with key {Key}", debugKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cache debug polyline to Redis");
            }
    }
}
