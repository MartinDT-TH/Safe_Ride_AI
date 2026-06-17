using System.ComponentModel.DataAnnotations;

namespace SafeRide.Infrastructure.ExternalServices.GoogleMaps;

public sealed class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";

    [Required]
    public string ApiKey { get; init; } = string.Empty;

    [Required]
    public string RoutesApiUrl { get; init; }
        = "https://routes.googleapis.com/directions/v2:computeRoutes";

    [Required]
    public string GeocodingApiUrl { get; init; }
        = "https://maps.googleapis.com/maps/api/geocode/json";
}
