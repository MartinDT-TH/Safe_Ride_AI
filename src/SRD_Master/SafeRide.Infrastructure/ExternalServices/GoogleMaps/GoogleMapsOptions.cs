namespace SafeRide.Infrastructure.ExternalServices.GoogleMaps;

public sealed class GoogleMapsOptions
{
    public const string SectionName = "MapServices:GoogleMaps";

    public string ApiKey { get; init; } = string.Empty;

    public string RoutesApiUrl { get; init; }
        = "https://routes.googleapis.com/directions/v2:computeRoutes";

    public string GeocodingApiUrl { get; init; }
        = "https://maps.googleapis.com/maps/api/geocode/json";

    public int TimeoutSeconds { get; init; } = 15;
}
