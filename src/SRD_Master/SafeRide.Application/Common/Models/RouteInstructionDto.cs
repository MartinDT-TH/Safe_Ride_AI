namespace SafeRide.Application.Common.Models;

public sealed class RouteInstructionDto
{
    public int Order { get; init; }
    public string? Text { get; init; }
    public double? DistanceMeters { get; init; }
    public double? DurationSeconds { get; init; }
    public LocationPoint? Location { get; init; }
}
