using System.ComponentModel.DataAnnotations;

namespace SafeRide.Infrastructure.ExternalServices.OpenRouteService;

public sealed class OpenRouteServiceOptions
{
    public const string SectionName = "MapServices:OpenRouteService";

    [Required]
    public string ApiKey { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = "https://api.openrouteservice.org";

    [Required]
    public string DirectionsApiUrl { get; init; }
        = "https://api.openrouteservice.org/v2/directions/driving-car";

    [Required]
    public string MatrixApiUrl { get; init; }
        = "https://api.openrouteservice.org/v2/matrix/driving-car";

    public string AutocompleteApiUrl { get; init; }
        = "https://api.openrouteservice.org/geocode/autocomplete";

    public string SearchApiUrl { get; init; }
        = "https://api.openrouteservice.org/geocode/search";

    public string ReverseApiUrl { get; init; }
        = "https://api.openrouteservice.org/geocode/reverse";

    public int TimeoutSeconds { get; init; } = 20;
}
