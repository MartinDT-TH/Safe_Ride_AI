using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Models;

public sealed class RouteEstimateResult
{
    public required MapProvider Provider { get; init; }

    public required double DistanceMeters { get; init; }
    public required double DurationSeconds { get; init; }

    // Backward-compatible computed properties (callers can keep using these)
    public double DistanceKm => Math.Round(DistanceMeters / 1000d, 2);
    public int DurationMinutes => (int)Math.Ceiling(DurationSeconds / 60d);

    public string? EncodedPolyline { get; init; }
    public string PolylineFormat { get; init; } = "polyline5";

    public IReadOnlyList<LocationPoint> Points { get; init; } = [];
    public IReadOnlyList<RouteInstructionDto> Instructions { get; init; } = [];
    public IReadOnlyList<RouteTollDto> Tolls { get; init; } = [];

    public double? TollAmount { get; init; }
    public string Currency { get; init; } = "VND";

    public string? Summary { get; init; }
    public string? RawProviderStatus { get; init; }

    public bool IsFallbackResult { get; init; }
    public MapProvider? FallbackFromProvider { get; init; }

    public DateTimeOffset CalculatedAt { get; init; } = DateTimeOffset.UtcNow;
}
