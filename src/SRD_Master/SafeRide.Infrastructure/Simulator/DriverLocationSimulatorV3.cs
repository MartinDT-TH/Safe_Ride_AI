using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace SafeRide.Infrastructure.Simulator;

public sealed class DriverLocationSimulatorV3
{
    private readonly ILogger<DriverLocationSimulatorV3> _logger;
    private const string ApiBaseUrl = "http://localhost:5026/api";
    private const string HubUrl = "http://localhost:5026/hubs/saferide";

    public DriverLocationSimulatorV3(ILogger<DriverLocationSimulatorV3> logger)
    {
        _logger = logger;
    }

    public async Task StartAsync(string driverPhone, long tripId, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting Driver Simulator for {Phone} on Trip {TripId}", driverPhone, tripId);

        try
        {
            // Wait for API to fully start up
            await Task.Delay(5000, ct);

            // 1. Authentication
            var token = await GetAccessTokenAsync(driverPhone);
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Simulator Error: Could not retrieve access token for {Phone}", driverPhone);
                return;
            }

            // 2. SignalR Connection
            var connection = new HubConnectionBuilder()
                .WithUrl(HubUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult((string?)token);
                })
                .WithAutomaticReconnect()
                .Build();

            await connection.StartAsync(ct);
            _logger.LogInformation("Simulator Connected to Hub. Status: {State}", connection.State);

            await connection.InvokeAsync("JoinTrip", tripId, ct);

            // 3. Movement Simulation
            var path = GetSimulationPath();
            foreach (var point in path)
            {
                if (ct.IsCancellationRequested) break;

                await connection.InvokeAsync("UpdateDriverLocation", point.Lat, point.Lng, ct);
                _logger.LogDebug("Simulator: Moved to {Lat}, {Lng}", point.Lat, point.Lng);

                await Task.Delay(3000, ct);
            }

            _logger.LogInformation("Simulator Finished for {Phone}", driverPhone);
            await connection.StopAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Simulator FATAL Error");
        }
    }

    private static async Task<string?> GetAccessTokenAsync(string phone)
    {
        using var client = new HttpClient();

        var sendOtpRes = await client.PostAsJsonAsync($"{ApiBaseUrl}/auth/send-otp", new { phoneNumber = phone });
        if (!sendOtpRes.IsSuccessStatusCode)
        {
            return null;
        }

        var verifyRes = await client.PostAsJsonAsync($"{ApiBaseUrl}/auth/verify-otp", new
        {
            phoneNumber = phone,
            otpCode = "111111",
            deviceId = "infra-simulator",
            deviceName = "Infrastructure Simulator"
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
        return new List<(double Lat, double Lng)>
        {
            (16.0611, 108.2275), (16.0600, 108.2240), (16.0590, 108.2210),
            (16.0580, 108.2180), (16.0560, 108.2150), (16.0540, 108.2120),
            (16.0520, 108.2090), (16.0500, 108.2050), (16.0485, 108.2010),
            (16.0475, 108.1970), (16.0472, 108.1920)
        };
    }
}
