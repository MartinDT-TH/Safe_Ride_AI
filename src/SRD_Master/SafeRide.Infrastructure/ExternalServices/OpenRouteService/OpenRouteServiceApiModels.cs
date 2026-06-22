using System.Text.Json.Serialization;

namespace SafeRide.Infrastructure.ExternalServices.OpenRouteService;

internal sealed class OpenRouteDirectionsResponse
{
    [JsonPropertyName("routes")]
    public List<OpenRouteRoute> Routes { get; init; } = [];
}

internal sealed class OpenRouteRoute
{
    [JsonPropertyName("geometry")]
    public string? Geometry { get; init; }
}

internal sealed class OpenRouteMatrixResponse
{
    [JsonPropertyName("distances")]
    public List<List<double?>>? Distances { get; init; }

    [JsonPropertyName("durations")]
    public List<List<double?>>? Durations { get; init; }
}

// ──────────────────────────── Geocode (Pelias) ────────────────────────────

internal sealed class OpenRouteGeocodeResponse
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("features")]
    public List<OpenRouteFeature>? Features { get; set; }
}

internal sealed class OpenRouteFeature
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("geometry")]
    public OpenRouteGeometry? Geometry { get; set; }

    [JsonPropertyName("properties")]
    public OpenRouteProperties? Properties { get; set; }
}

internal sealed class OpenRouteGeometry
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// For Point geometry, coordinates are [longitude, latitude].
    /// </summary>
    [JsonPropertyName("coordinates")]
    public List<double>? Coordinates { get; set; }
}

internal sealed class OpenRouteProperties
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    [JsonPropertyName("county")]
    public string? County { get; set; }

    [JsonPropertyName("locality")]
    public string? Locality { get; set; }

    [JsonPropertyName("street")]
    public string? Street { get; set; }

    [JsonPropertyName("housenumber")]
    public string? HouseNumber { get; set; }
}
