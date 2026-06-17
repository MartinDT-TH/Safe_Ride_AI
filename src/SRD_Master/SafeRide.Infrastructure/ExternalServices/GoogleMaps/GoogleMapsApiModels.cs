using System.Text.Json.Serialization;

namespace SafeRide.Infrastructure.ExternalServices.GoogleMaps;

internal sealed class GoogleRoutesResponse
{
    [JsonPropertyName("routes")]
    public List<GoogleRoute> Routes { get; init; } = [];
}

internal sealed class GoogleRoute
{
    [JsonPropertyName("distanceMeters")]
    public int DistanceMeters { get; init; }

    [JsonPropertyName("duration")]
    public string Duration { get; init; } = string.Empty;

    [JsonPropertyName("polyline")]
    public GooglePolyline? Polyline { get; init; }
}

internal sealed class GooglePolyline
{
    [JsonPropertyName("encodedPolyline")]
    public string? EncodedPolyline { get; init; }
}
