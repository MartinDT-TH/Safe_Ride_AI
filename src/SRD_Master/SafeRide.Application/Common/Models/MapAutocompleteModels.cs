namespace SafeRide.Application.Common.Models;

public sealed class MapAutocompleteRequest
{
    public string Query { get; init; } = string.Empty;
    public double? LocationLat { get; init; }
    public double? LocationLng { get; init; }
    public string? SessionToken { get; init; }
}

public sealed class MapGeocodeRequest
{
    public string Query { get; init; } = string.Empty;
}

public sealed class MapSuggestionDto
{
    public string ProviderPlaceId { get; init; } = string.Empty;
    public string PrimaryText { get; init; } = string.Empty;
    public string SecondaryText { get; init; } = string.Empty;
    public double? Lat { get; init; }
    public double? Lng { get; init; }
}

public sealed class MapPlaceDto
{
    public string ProviderPlaceId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public double Lat { get; init; }
    public double Lng { get; init; }
}
