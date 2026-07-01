using System;
using System.Text.Json;

namespace SafeRide.Infrastructure.Simulator;

public static class SimulatorConsoleOutput
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Print(string category, string action, object? data = null, bool enabled = true)
    {
        if (!enabled) return;

        Console.WriteLine("--------------------------------------------------");
        Console.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC] {category}[{action}]");
        
        if (data != null)
        {
            try
            {
                var json = JsonSerializer.Serialize(data, JsonOptions);
                Console.WriteLine(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[JSON Serialization Error]: {ex.Message}");
            }
        }
        Console.WriteLine("--------------------------------------------------");
    }
}
