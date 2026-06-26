namespace SafeRide.Infrastructure.ExternalServices.VietMap;

public sealed class VietMapOptions
{
    public const string SectionName = "MapServices:VietMap";

    public string ApiKey { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = "https://maps.vietmap.vn";

    /// <summary>Route v3 endpoint path.</summary>
    public string RouteApiPath { get; init; } = "/api/route";

    /// <summary>Autocomplete v4 endpoint path.</summary>
    public string AutocompleteApiPath { get; init; } = "/api/autocomplete/v4";

    /// <summary>Geocode/Search v4 endpoint path.</summary>
    public string GeocodeApiPath { get; init; } = "/api/search/v4";

    /// <summary>Place detail v4 endpoint path.</summary>
    public string PlaceApiPath { get; init; } = "/api/place/v4";

    /// <summary>Reverse geocode v4 endpoint path.</summary>
    public string ReverseApiPath { get; init; } = "/api/reverse/v4";

    public int TimeoutSeconds { get; init; } = 15;
}
