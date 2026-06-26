using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Models;

public sealed class RouteEstimateRequest
{
    public required LocationPoint Origin { get; init; }
    public required LocationPoint Destination { get; init; }

    public MapProvider Provider { get; init; } = MapProvider.Auto;
    public MapTravelMode TravelMode { get; init; } = MapTravelMode.Car;

    public bool IncludePolyline { get; init; } = true;
    public bool IncludeInstructions { get; init; } = false;
    public bool IncludeTolls { get; init; } = false;
    public bool PointsEncoded { get; init; } = true;

    public string? Language { get; init; } = "vi";

    /// <summary>
    /// Cho biết ngữ cảnh gọi để phục vụ logging/tracing.
    /// Ví dụ: "CreateBooking", "RefreshEta", "DriverMatching", "AdminPreview", "ScheduledBooking"
    /// </summary>
    public string? RequestSource { get; init; }
}
