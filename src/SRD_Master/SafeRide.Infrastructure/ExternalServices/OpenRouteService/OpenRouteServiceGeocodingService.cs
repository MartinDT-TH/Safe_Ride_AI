using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;

namespace SafeRide.Infrastructure.ExternalServices.OpenRouteService;

internal sealed class OpenRouteServiceGeocodingService : IMapGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly OpenRouteServiceOptions _options;
    private readonly ILogger<OpenRouteServiceGeocodingService> _logger;

    public OpenRouteServiceGeocodingService(
        HttpClient httpClient,
        IOptions<OpenRouteServiceOptions> options,
        ILogger<OpenRouteServiceGeocodingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    private string BuildUrl(string path, Dictionary<string, string?> parameters)
    {
        var query = new List<string> { $"api_key={_options.ApiKey}" };
        foreach (var (key, value) in parameters)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                query.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
            }
        }
        
        string fullUrl;
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            fullUrl = path;
        }
        else
        {
            var baseUrl = _options.BaseUrl.TrimEnd('/');
            var relativePath = path.StartsWith('/') ? path : $"/{path}";
            fullUrl = $"{baseUrl}{relativePath}";
        }

        return $"{fullUrl}?{string.Join("&", query)}";
    }

    private async Task<T?> GetFromJsonAsync<T>(string url, string context, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenRouteService Geocoding API failed for {Context}. URL: {Url}", context, url.Replace(_options.ApiKey, "***"));
            throw;
        }
    }

    public async Task<IReadOnlyList<MapSuggestionDto>> AutocompleteAsync(MapAutocompleteRequest request, CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["text"] = request.Query
        };

        if (request.LocationLat.HasValue && request.LocationLng.HasValue)
        {
            parameters["focus.point.lat"] = request.LocationLat.Value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
            parameters["focus.point.lon"] = request.LocationLng.Value.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
        }

        var url = BuildUrl(_options.AutocompleteApiUrl, parameters);
        _logger.LogDebug("ORS Autocomplete: query={Query}", request.Query);

        var result = await GetFromJsonAsync<OpenRouteGeocodeResponse>(url, "ORS Autocomplete", cancellationToken);
        if (result?.Features == null || result.Features.Count == 0) return Array.Empty<MapSuggestionDto>();

        return result.Features.Select(f =>
        {
            double? lat = null;
            double? lng = null;
            if (f.Geometry?.Coordinates != null && f.Geometry.Coordinates.Count >= 2)
            {
                lng = f.Geometry.Coordinates[0];
                lat = f.Geometry.Coordinates[1];
            }

            var name = f.Properties?.Name ?? string.Empty;
            var address = f.Properties?.Label ?? string.Empty;
            
            // Encode the Place ID with lat/lng to avoid fetching later
            var providerPlaceId = f.Properties?.Id ?? Guid.NewGuid().ToString();
            if (lat.HasValue && lng.HasValue)
            {
                providerPlaceId = $"{lat.Value},{lng.Value}|{name}|{address}";
            }

            return new MapSuggestionDto
            {
                ProviderPlaceId = providerPlaceId,
                PrimaryText = name,
                SecondaryText = address,
                Lat = lat,
                Lng = lng
            };
        }).ToList();
    }

    public async Task<MapPlaceDto?> GetPlaceDetailAsync(string providerPlaceId, CancellationToken cancellationToken = default)
    {
        // Decode our custom ID format: "Lat,Lng|Name|Address"
        if (providerPlaceId.Contains('|'))
        {
            var parts = providerPlaceId.Split('|');
            var coords = parts[0].Split(',');
            if (coords.Length == 2 && 
                double.TryParse(coords[0], System.Globalization.CultureInfo.InvariantCulture, out var lat) && 
                double.TryParse(coords[1], System.Globalization.CultureInfo.InvariantCulture, out var lng))
            {
                return new MapPlaceDto
                {
                    ProviderPlaceId = providerPlaceId,
                    Name = parts.Length > 1 ? parts[1] : string.Empty,
                    Address = parts.Length > 2 ? parts[2] : string.Empty,
                    Lat = lat,
                    Lng = lng
                };
            }
        }

        // Fallback: Use Search API with the ProviderPlaceId as text query
        var results = await GeocodeAsync(new MapGeocodeRequest { Query = providerPlaceId }, cancellationToken);
        return results.FirstOrDefault();
    }

    public async Task<IReadOnlyList<MapPlaceDto>> GeocodeAsync(MapGeocodeRequest request, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(_options.SearchApiUrl, new Dictionary<string, string?>
        {
            ["text"] = request.Query
        });

        var result = await GetFromJsonAsync<OpenRouteGeocodeResponse>(url, "ORS Geocode", cancellationToken);
        if (result?.Features == null) return Array.Empty<MapPlaceDto>();

        return result.Features.Select(f =>
        {
            double lat = 0;
            double lng = 0;
            if (f.Geometry?.Coordinates != null && f.Geometry.Coordinates.Count >= 2)
            {
                lng = f.Geometry.Coordinates[0];
                lat = f.Geometry.Coordinates[1];
            }

            return new MapPlaceDto
            {
                ProviderPlaceId = f.Properties?.Id ?? Guid.NewGuid().ToString(),
                Name = f.Properties?.Name ?? string.Empty,
                Address = f.Properties?.Label ?? string.Empty,
                Lat = lat,
                Lng = lng
            };
        }).ToList();
    }

    public async Task<MapPlaceDto?> ReverseGeocodeAsync(double lat, double lng, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(_options.ReverseApiUrl, new Dictionary<string, string?>
        {
            ["point.lat"] = lat.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
            ["point.lon"] = lng.ToString("G", System.Globalization.CultureInfo.InvariantCulture)
        });

        var result = await GetFromJsonAsync<OpenRouteGeocodeResponse>(url, "ORS Reverse Geocode", cancellationToken);
        var first = result?.Features?.FirstOrDefault();
        if (first == null) return null;

        double finalLat = lat;
        double finalLng = lng;
        if (first.Geometry?.Coordinates != null && first.Geometry.Coordinates.Count >= 2)
        {
            finalLng = first.Geometry.Coordinates[0];
            finalLat = first.Geometry.Coordinates[1];
        }

        return new MapPlaceDto
        {
            ProviderPlaceId = first.Properties?.Id ?? Guid.NewGuid().ToString(),
            Name = first.Properties?.Name ?? string.Empty,
            Address = first.Properties?.Label ?? string.Empty,
            Lat = finalLat,
            Lng = finalLng
        };
    }
}
