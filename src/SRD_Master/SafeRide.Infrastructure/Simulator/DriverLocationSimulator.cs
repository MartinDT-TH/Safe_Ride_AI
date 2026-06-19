using System.Text.Json;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Infrastructure.Redis;

/*
 * Driver Location Simulator for SafeRide
 *
 * This simulator simulates a driver moving from a pickup point to a destination point
 * by updating Redis and publishing realtime SignalR events.
 *
 * Useful for testing:
 * - Flutter tracking screen
 * - Driver marker movement on Google Map
 * - Nearby drivers radar
 * - Realtime DriverLocationUpdated event
 *
 * Usage:
 * 1. Ensure Redis online configuration is already working in the main system.
 * 2. Register DriverLocationSimulator in DI.
 * 3. Enable simulator from appsettings.Development.json if needed.
 * 4. Update the DriverId, TripId, and CustomerId to match your test data.
 */

namespace SafeRide.Infrastructure.Simulator;

public class DriverLocationSimulator
{
    private readonly IRedisService _redisService;
    private readonly IRealtimeNotificationService _realtimeNotificationService;

    public DriverLocationSimulator(
        IRedisService redisService,
        IRealtimeNotificationService realtimeNotificationService)
    {
        _redisService = redisService;
        _realtimeNotificationService = realtimeNotificationService;
    }

    // Configuration
    private static readonly Guid DriverId =
        Guid.Parse("10000000-0000-0000-0000-000000000001"); // Replace with actual Driver ID

    private const long TripId = 9; // Replace with actual Trip ID

    private static readonly Guid? CustomerId =
        Guid.Parse("8D3E426F-A485-4D72-981D-4056A13C8387"); // Optional: Set if you want to test customer-specific notifications

    private const string GeoKey = "sr:geo:drivers:online";

    // Movement configuration
    // Increase this value to make the route smoother.
    private const int StepsPerSegment = 5; // 12

    // Increase this value to make the driver move slower on the map.
    private static readonly TimeSpan MovementDelay = TimeSpan.FromSeconds(1); // 3

    private static string LocationKey(Guid id) => $"sr:driver:location:{id}";
    private static string OnlineKey(Guid id) => $"sr:driver:online:{id}";
    private static string StatusKey(Guid id) => $"sr:driver:status:{id}";

    public async Task RunAsync()
    {
        Console.WriteLine("--- SafeRide Driver Location Simulator ---");
        Console.WriteLine($"Simulating Driver: {DriverId}");
        Console.WriteLine($"Simulating Trip: {TripId}");

        /*
         * Sample path based on real map direction:
         *
         * Start:
         * 16.070800275793697, 108.21351238136269
         *
         * End:
         * 16.05702162564365, 108.20252646698634
         *
         * This path is manually shaped to look more natural on the map.
         * For production-quality simulation, use Google Routes API polyline.
         */
        var waypoints = new List<(double Lat, double Lng)>
        {
            // Start point
            (16.07728274857859, 108.22024155731957),

            // Move south / southwest from the start point
            (16.076850, 108.219920),
            (16.076420, 108.219600),
            (16.075980, 108.219280),
            (16.075540, 108.218950),
            (16.075100, 108.218620),
            (16.074650, 108.218280),
            (16.074200, 108.217940),
            (16.073760, 108.217600),
            (16.073300, 108.217250),
            (16.072850, 108.216900),
            (16.072400, 108.216560),
            (16.071950, 108.216200),
            (16.071500, 108.215850),
            (16.071050, 108.215500),
            (16.070600, 108.215150),
            (16.070150, 108.214800),
            (16.069700, 108.214450),
            (16.069250, 108.214100),
            (16.068800, 108.213750),

            // Continue toward airport direction
            (16.068350, 108.213390),
            (16.067900, 108.213030),
            (16.067450, 108.212670),
            (16.067000, 108.212310),
            (16.066550, 108.211950),
            (16.066100, 108.211590),
            (16.065650, 108.211220),
            (16.065200, 108.210850),
            (16.064750, 108.210480),
            (16.064300, 108.210100),
            (16.063850, 108.209720),
            (16.063400, 108.209330),
            (16.062950, 108.208940),
            (16.062500, 108.208540),
            (16.062050, 108.208130),
            (16.061600, 108.207700),

            // Turn gradually toward Da Nang Airport
            (16.061150, 108.207260),
            (16.060720, 108.206820),
            (16.060300, 108.206360),
            (16.059900, 108.205900),
            (16.059520, 108.205430),
            (16.059150, 108.204950),
            (16.058800, 108.204460),
            (16.058470, 108.203970),
            (16.058150, 108.203480),
            (16.057850, 108.202980),
            (16.057450, 108.202730),

            // End point - near Da Nang Airport
            (16.05702162564365, 108.20252646698634)
        };

        var path = BuildSmoothPath(waypoints, StepsPerSegment);

        // Set Driver Online Status
        await _redisService.SetAsync(
            OnlineKey(DriverId),
            "1",
            TimeSpan.FromMinutes(30));

        await _redisService.SetAsync(
            StatusKey(DriverId),
            "Online",
            TimeSpan.FromMinutes(30));

        Console.WriteLine("Driver is now ONLINE in Redis.");
        Console.WriteLine("Starting movement simulation...");
        Console.WriteLine($"Total simulated points: {path.Count}");
        Console.WriteLine($"Movement delay: {MovementDelay.TotalSeconds} seconds per point");

        foreach (var point in path)
        {
            var utcNow = DateTime.UtcNow;

            // 1. Update Detailed Location Cache
            var cache = new
            {
                driverId = DriverId,
                latitude = point.Lat,
                longitude = point.Lng,
                updatedAt = utcNow
            };

            await _redisService.SetAsync(
                LocationKey(DriverId),
                JsonSerializer.Serialize(cache),
                TimeSpan.FromMinutes(10));

            // 2. Update Geo Spatial Index (Important for Nearby Drivers API)
            // Note: Redis GEOADD expects Longitude then Latitude
            await _redisService.GeoAddAsync(
                GeoKey,
                point.Lng,
                point.Lat,
                DriverId.ToString());

            // 3. Publish realtime event to SignalR clients
            // This allows the Flutter tracking screen to receive DriverLocationUpdated.
            var realtimeEvent = new DriverLocationUpdatedEvent(
                DriverId,
                CustomerId,
                TripId,
                point.Lat,
                point.Lng,
                utcNow);

            await _realtimeNotificationService
                .PublishDriverLocationUpdatedAsync(realtimeEvent);

            Console.WriteLine($"[{utcNow:HH:mm:ss}] Moved to: {point.Lat}, {point.Lng}");
            Console.WriteLine($"[SignalR] Sent DriverLocationUpdated for Trip {TripId}");

            // Wait before next movement update
            await Task.Delay(MovementDelay);
        }

        Console.WriteLine("Simulation completed.");
    }

    /*
     * BuildSmoothPath creates intermediate points between waypoints.
     *
     * Why this is needed:
     * - Without smoothing, the marker jumps from one waypoint to another.
     * - With smoothing, the driver marker moves gradually on the map.
     *
     * Increase stepsPerSegment for smoother and slower-looking movement.
     */
    private static List<(double Lat, double Lng)> BuildSmoothPath(
        List<(double Lat, double Lng)> waypoints,
        int stepsPerSegment)
    {
        var smoothPath = new List<(double Lat, double Lng)>();

        if (waypoints.Count == 0)
        {
            return smoothPath;
        }

        if (waypoints.Count == 1)
        {
            smoothPath.Add(waypoints[0]);
            return smoothPath;
        }

        for (var i = 0; i < waypoints.Count - 1; i++)
        {
            var start = waypoints[i];
            var end = waypoints[i + 1];

            for (var step = 0; step < stepsPerSegment; step++)
            {
                var t = (double)step / stepsPerSegment;

                var lat = start.Lat + (end.Lat - start.Lat) * t;
                var lng = start.Lng + (end.Lng - start.Lng) * t;

                smoothPath.Add((lat, lng));
            }
        }

        smoothPath.Add(waypoints[^1]);

        return smoothPath;
    }
}