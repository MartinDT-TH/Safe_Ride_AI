using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client; //đã cài thêm để demo giả lập kết nối thực Driver với BE qua SignalR

namespace SafeRide.Infrastructure.Simulator;

public class DriverLocationSimulatorV2
{
    // --- CONFIGURATION ---
    private const string ApiBaseUrl = "http://localhost:5026/api";
    private const string HubUrl = "http://localhost:5026/hubs/saferide";

    // Replace with a valid driver's phone number in your DB
    private const string DriverPhone = "0987654321";

    // Replace with a valid trip ID if you want to test trip-specific tracking
    private const long ActiveTripId = 1;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("--- SafeRide Real-time Driver Simulator V2 ---");

        try
        {
            // 1. Authentication
            Console.WriteLine($"[1/3] Logging in as driver ({DriverPhone})...");
            var token = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("Error: Could not retrieve access token. Check API and phone number.");
                return;
            }

            // 2. SignalR Connection
            Console.WriteLine("[2/3] Connecting to SignalR Hub...");
            var connection = new HubConnectionBuilder()
                .WithUrl(HubUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult((string?)token);
                })
                .WithAutomaticReconnect()
                .Build();

            await connection.StartAsync();
            Console.WriteLine("Connected! Status: " + connection.State);

            // Optional: Join the trip group if needed by BE logic
            await connection.InvokeAsync("JoinTrip", ActiveTripId);

            // 3. Movement Simulation
            Console.WriteLine("[3/3] Starting movement simulation (Dragon Bridge -> Airport)...");

            var path = GetSimulationPath();
            foreach (var point in path)
            {
                // Send location update to Hub
                await connection.InvokeAsync("UpdateDriverLocation", point.Lat, point.Lng);

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Published: {point.Lat}, {point.Lng}");

                await Task.Delay(3000); // 3 seconds interval
            }

            Console.WriteLine("Simulation finished. Press any key to disconnect.");
            Console.ReadKey();
            await connection.StopAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FATAL ERROR: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"Inner: {ex.InnerException.Message}");
        }
    }

    private static async Task<string?> GetAccessTokenAsync()
    {
        using var client = new HttpClient();

        // Step A: Send OTP
        var sendOtpRes = await client.PostAsJsonAsync($"{ApiBaseUrl}/auth/send-otp", new { phoneNumber = DriverPhone });
        if (!sendOtpRes.IsSuccessStatusCode)
        {
            return null;
        }

        // Step B: Verify OTP (Assuming '111111' for local dev/test environment)
        var verifyRes = await client.PostAsJsonAsync($"{ApiBaseUrl}/auth/verify-otp", new
        {
            phoneNumber = DriverPhone,
            otpCode = "111111",
            deviceId = "simulator-pc",
            deviceName = "Windows Simulator"
        });

        if (!verifyRes.IsSuccessStatusCode)
        {
            return null;
        }

        var loginData = await verifyRes.Content.ReadFromJsonAsync<JsonElement>();
        return loginData.ValueKind == JsonValueKind.Object &&
               loginData.TryGetProperty("accessToken", out var tokenProperty)
            ? tokenProperty.GetString()
            : null;
    }

    private static List<(double Lat, double Lng)> GetSimulationPath()
    {
        // Sample path in Da Nang
        return new List<(double Lat, double Lng)>
        {
            (16.0611, 108.2275), // Start: Dragon Bridge
            (16.0600, 108.2240),
            (16.0590, 108.2210),
            (16.0580, 108.2180),
            (16.0560, 108.2150),
            (16.0540, 108.2120),
            (16.0520, 108.2090),
            (16.0500, 108.2050),
            (16.0485, 108.2010),
            (16.0475, 108.1970), // Near Airport
            (16.0472, 108.1920)  // Destination/Arrived
        };
    }
}
#if false
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

/*
 * SafeRide Real-time Driver Simulator (V2 - SignalR)
 *
 * This version connects to the backend as a real Driver via SignalR.
 * It simulates the complete flow: Login -> Connect Hub -> Stream Location.
 *
 * NOTE: This file is kept as reference-only because it requires SignalR client support
 * that is not currently part of the project dependencies.
 */

namespace SafeRide.Infrastructure.Simulator;

public class DriverLocationSimulatorV2
{
    // --- CONFIGURATION ---
    private const string ApiBaseUrl = "http://localhost:5026/api";
    private const string HubUrl = "http://localhost:5026/hubs/saferide";

    // Replace with a valid driver's phone number in your DB
    private const string DriverPhone = "0987654321";

    // Replace with a valid trip ID if you want to test trip-specific tracking
    private const long ActiveTripId = 1;

    public static async Task Main(string[] args)
    {
        Console.WriteLine("--- SafeRide Real-time Driver Simulator V2 ---");

        try
        {
            // 1. Authentication
            Console.WriteLine($"[1/3] Logging in as driver ({DriverPhone})...");
            var token = await GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("Error: Could not retrieve access token. Check API and phone number.");
                return;
            }

            // 2. SignalR Connection
            Console.WriteLine("[2/3] Connecting to SignalR Hub...");
            var connection = new HubConnectionBuilder()
                .withUrl(HubUrl, options => {
                    options.AccessTokenProvider = () => Task.FromResult(token);
                })
                .WithAutomaticReconnect()
                .Build();

            await connection.StartAsync();
            Console.WriteLine("Connected! Status: " + connection.State);

            // Optional: Join the trip group if needed by BE logic
            await connection.InvokeAsync("JoinTrip", ActiveTripId);

            // 3. Movement Simulation
            Console.WriteLine("[3/3] Starting movement simulation (Dragon Bridge -> Airport)...");

            var path = GetSimulationPath();
            foreach (var point in path)
            {
                // Send location update to Hub
                // This triggers the BE logic to update Redis AND broadcast to Customer
                await connection.InvokeAsync("UpdateDriverLocation", point.Lat, point.Lng);

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Published: {point.Lat}, {point.Lng}");

                await Task.Delay(3000); // 3 seconds interval
            }

            Console.WriteLine("Simulation finished. Press any key to disconnect.");
            Console.ReadKey();
            await connection.StopAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FATAL ERROR: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"Inner: {ex.InnerException.Message}");
        }
    }

    private static async Task<string?> GetAccessTokenAsync()
    {
        using var client = new HttpClient();

        // Step A: Send OTP
        var sendOtpRes = await client.PostAsJsonAsync($"{ApiBaseUrl}/auth/send-otp", new { phoneNumber = DriverPhone });
        if (!sendOtpRes.IsSuccessStatusCode)
        {
            return null;
        }

        // Step B: Verify OTP (Assuming '111111' for local dev/test environment)
        var verifyRes = await client.PostAsJsonAsync($"{ApiBaseUrl}/auth/verify-otp", new
        {
            phoneNumber = DriverPhone,
            otpCode = "111111",
            deviceId = "simulator-pc",
            deviceName = "Windows Simulator"
        });

        if (!verifyRes.IsSuccessStatusCode)
        {
            return null;
        }

        var loginData = await verifyRes.Content.ReadFromJsonAsync<JsonElement>();
        return loginData.ValueKind == JsonValueKind.Object &&
               loginData.TryGetProperty("accessToken", out var tokenProperty)
            ? tokenProperty.GetString()
            : null;
    }

    private static List<(double Lat, double Lng)> GetSimulationPath()
    {
        // Sample path in Da Nang
        return new List<(double Lat, double Lng)>
        {
            (16.0611, 108.2275), // Start: Dragon Bridge
            (16.0600, 108.2240),
            (16.0590, 108.2210),
            (16.0580, 108.2180),
            (16.0560, 108.2150),
            (16.0540, 108.2120),
            (16.0520, 108.2090),
            (16.0500, 108.2050),
            (16.0485, 108.2010),
            (16.0475, 108.1970), // Near Airport
            (16.0472, 108.1920)  // Destination/Arrived
        };
    }
}
#endif
