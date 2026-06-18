using System.Text.Json;
using StackExchange.Redis;

/*
 * Driver Location Simulator for SafeRide
 *
 * This script simulates a driver moving towards a pickup point by updating Redis directly.
 * Useful for testing the Flutter tracking screen and nearby drivers' radar.
 *
 * Usage:
 * 1. Ensure Redis is running (default: localhost:6379).
 * 2. Run this script using 'dotnet script' or include it in a Console App.
 * 3. Update the 'DriverId' to match a driver in your database.
 */

namespace SafeRide.Simulator;

public class DriverLocationSimulator
{
    public static async Task Main(string[] args)
    {
        await RunAsync();
    }

    // Configuration
    private const string RedisConnectionString = "localhost:6379";
    private static readonly Guid DriverId = Guid.Parse("10000000-0000-0000-0000-000000000003"); // Replace with actual Driver ID
    private const string GeoKey = "sr:geo:drivers:online";
    private static string LocationKey(Guid id) => $"sr:driver:location:{id}";
    private static string OnlineKey(Guid id) => $"sr:driver:online:{id}";
    private static string StatusKey(Guid id) => $"sr:driver:status:{id}";

    public static async Task RunAsync()
    {
        Console.WriteLine("--- SafeRide Driver Location Simulator ---");
        Console.WriteLine($"Simulating Driver: {DriverId}");

        var redis = await ConnectionMultiplexer.ConnectAsync(RedisConnectionString);
        var db = redis.GetDatabase();

        // Path from Dragon Bridge to Da Nang Airport (Sample coordinates)
        var path = new List<(double Lat, double Lng)>
        {
            (16.0611, 108.2275), // Dragon Bridge
            (16.0595, 108.2225),
            (16.0580, 108.2180),
            (16.0565, 108.2140),
            (16.0550, 108.2100),
            (16.0535, 108.2060),
            (16.0520, 108.2020),
            (16.0505, 108.1980),
            (16.0490, 108.1940),
            (16.0475, 108.1900)  // Near Airport
        };

        // Set Driver Online Status
        await db.StringSetAsync(OnlineKey(DriverId), "1", TimeSpan.FromMinutes(30));
        await db.StringSetAsync(StatusKey(DriverId), "Online", TimeSpan.FromMinutes(30));

        Console.WriteLine("Driver is now ONLINE in Redis.");
        Console.WriteLine("Starting movement simulation...");

        foreach (var point in path)
        {
            var utcNow = DateTime.UtcNow;

            // 1. Update Detailed Location Cache
            var cache = new
            {
                DriverId = DriverId,
                Latitude = point.Lat,
                Longitude = point.Lng,
                UpdatedAt = utcNow
            };

            await db.StringSetAsync(
                LocationKey(DriverId),
                JsonSerializer.Serialize(cache),
                TimeSpan.FromMinutes(10));

            // 2. Update Geo Spatial Index (Important for Nearby Drivers API)
            // Note: Redis GEOADD expects Longitude then Latitude
            await db.GeoAddAsync(GeoKey, point.Lng, point.Lat, DriverId.ToString());

            Console.WriteLine($"[{utcNow:HH:mm:ss}] Moved to: {point.Lat}, {point.Lng}");

            // Wait for 3 seconds before next move
            await Task.Delay(3000);
        }

        Console.WriteLine("Simulation completed.");
    }
}
