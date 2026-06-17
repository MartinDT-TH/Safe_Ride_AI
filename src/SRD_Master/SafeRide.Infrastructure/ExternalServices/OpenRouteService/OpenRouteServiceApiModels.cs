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
