namespace SafeRide.Application.Common.Models;

public sealed class RouteTollDto
{
    public string? Name { get; init; }
    public LocationPoint? Location { get; init; }
    public decimal? Amount { get; init; }
    public string Currency { get; init; } = "VND";
}
