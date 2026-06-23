using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;

namespace SafeRide.Infrastructure.ExternalServices.VietMap;

internal sealed class VietMapGeocodingService : IMapGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly VietMapOptions _options;
    private readonly ILogger<VietMapGeocodingService> _logger;

    public VietMapGeocodingService(
        HttpClient httpClient,
        IOptions<VietMapOptions> options,
        ILogger<VietMapGeocodingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
    }

    private string BuildUrl(string path, Dictionary<string, string?> parameters)
    {
        var query = new List<string> { $"apikey={_options.ApiKey}" };
        foreach (var (key, value) in parameters)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                query.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
            }
        }
        return $"{path}?{string.Join("&", query)}";
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
            _logger.LogError(ex, "VietMap Geocoding API failed for {Context}. URL: {Url}", context, url.Replace(_options.ApiKey, "***"));
            throw;
        }
    }

    // Autocomplete v4
    public async Task<IReadOnlyList<MapSuggestionDto>> AutocompleteAsync(
        MapAutocompleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(_options.AutocompleteApiPath, new Dictionary<string, string?>
        {
            ["text"] = request.Query,
            ["focus"] = request.LocationLat.HasValue && request.LocationLng.HasValue 
                        ? $"{request.LocationLat.Value},{request.LocationLng.Value}" 
                        : null,
            ["display_type"] = "3"
        });

        _logger.LogDebug("VietMap Autocomplete: query={Query}", request.Query);

        var results = await GetFromJsonAsync<List<VietMapAutocompleteResult>>(url, "VietMap autocomplete", cancellationToken);

        if (results == null || results.Count == 0)
        {
            _logger.LogDebug("VietMap autocomplete API returned empty response for query={Query}", request.Query);
            return Array.Empty<MapSuggestionDto>();
        }

        return results.Select(r => new MapSuggestionDto
        {
            ProviderPlaceId = r.RefId ?? Guid.NewGuid().ToString(),
            PrimaryText = FirstNonEmpty(r.Name, r.Display, r.Address),
            SecondaryText = SecondaryText(r.Name, r.Display, r.Address),
            Lat = r.Lat,
            Lng = r.Lng
        }).ToList();
    }

    public async Task<MapPlaceDto?> GetPlaceDetailAsync(string providerPlaceId, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(_options.PlaceApiPath, new Dictionary<string, string?>
        {
            ["refid"] = providerPlaceId
        });

        var result = await GetFromJsonAsync<VietMapPlaceDetailResult>(url, "VietMap place detail", cancellationToken);
        if (result == null) return null;

        return new MapPlaceDto
        {
            ProviderPlaceId = result.RefId ?? providerPlaceId,
            Name = result.Name ?? result.Display ?? string.Empty,
            Address = FirstNonEmpty(result.Display, result.Address, result.Name),
            Lat = result.Lat,
            Lng = result.Lng
        };
    }

    public async Task<IReadOnlyList<MapPlaceDto>> GeocodeAsync(MapGeocodeRequest request, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(_options.GeocodeApiPath, new Dictionary<string, string?>
        {
            ["text"] = request.Query,
            ["display_type"] = "3"
        });

        var results = await GetFromJsonAsync<List<VietMapGeocodeResult>>(url, "VietMap geocode", cancellationToken);

        if (results == null) return Array.Empty<MapPlaceDto>();

        var places = new List<MapPlaceDto>(results.Count);
        foreach (var result in results)
        {
            var place = new MapPlaceDto
            {
                ProviderPlaceId = result.RefId ?? Guid.NewGuid().ToString(),
                Name = result.Name ?? result.Display ?? string.Empty,
                Address = FirstNonEmpty(result.Display, result.Address, result.Name),
                Lat = result.Lat,
                Lng = result.Lng
            };

            if (IsMissingCoordinates(place) && !string.IsNullOrWhiteSpace(result.RefId))
            {
                var detail = await GetPlaceDetailAsync(result.RefId, cancellationToken);
                if (detail is not null && !IsMissingCoordinates(detail))
                {
                    place = new MapPlaceDto
                    {
                        ProviderPlaceId = place.ProviderPlaceId,
                        Name = FirstNonEmpty(place.Name, detail.Name),
                        Address = FirstNonEmpty(place.Address, detail.Address),
                        Lat = detail.Lat,
                        Lng = detail.Lng
                    };
                }
            }

            places.Add(place);
        }

        return places;
    }

    public async Task<MapPlaceDto?> ReverseGeocodeAsync(double lat, double lng, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(_options.ReverseApiPath, new Dictionary<string, string?>
        {
            ["lat"] = lat.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
            ["lng"] = lng.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
            ["display_type"] = "3"
        });

        var results = await GetFromJsonAsync<List<VietMapReverseResult>>(url, "VietMap reverse", cancellationToken);
        var first = results?.FirstOrDefault();
        if (first == null) return null;

        return new MapPlaceDto
        {
            ProviderPlaceId = first.RefId ?? Guid.NewGuid().ToString(),
            Name = first.Name ?? first.Display ?? string.Empty,
            Address = FirstNonEmpty(first.Display, first.Address, first.Name),
            Lat = first.Lat != 0 ? first.Lat : lat,
            Lng = first.Lng != 0 ? first.Lng : lng
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string SecondaryText(string? name, string? display, string? address)
    {
        var primary = FirstNonEmpty(name, display, address);
        var secondary = FirstNonEmpty(display, address);
        return string.Equals(primary, secondary, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : secondary;
    }

    private static bool IsMissingCoordinates(MapPlaceDto place) =>
        place.Lat == 0 && place.Lng == 0;
}
