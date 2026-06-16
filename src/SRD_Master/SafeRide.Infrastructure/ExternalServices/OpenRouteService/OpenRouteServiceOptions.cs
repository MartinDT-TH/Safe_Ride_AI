using System.ComponentModel.DataAnnotations;

namespace SafeRide.Infrastructure.ExternalServices.OpenRouteService;

public sealed class OpenRouteServiceOptions
{
    public const string SectionName = "OpenRouteService";

    [Required]
    public string ApiKey { get; init; } = string.Empty;

    [Required]
    public string DirectionsApiUrl { get; init; }
        = "https://api.openrouteservice.org/v2/directions/driving-car";

    [Required]
    public string MatrixApiUrl { get; init; }
        = "https://api.openrouteservice.org/v2/matrix/driving-car";
}
