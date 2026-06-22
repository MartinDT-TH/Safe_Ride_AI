using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Domain.Enums;

namespace SafeRide.API.Controllers;

/// <summary>
/// Map services proxy — Flutter calls these endpoints, backend calls VietMap (or configured provider).
/// Flutter never has direct access to VietMap API keys.
/// Active provider is controlled by MapServices:PrimaryProvider in appsettings
/// (or overridden at startup in Program.cs for dev testing).
/// </summary>
[ApiController]
[Route("api/maps")]
[AllowAnonymous]
public sealed class MapsController : ControllerBase
{
    private readonly IMapRoutingService _routingService;
    private readonly IMapGeocodingService _geocodingService;

    public MapsController(
        IMapRoutingService routingService,
        IMapGeocodingService geocodingService)
    {
        _routingService = routingService;
        _geocodingService = geocodingService;
    }

    /// <summary>
    /// Estimate a route (distance, duration, polyline) between two coordinates.
    /// </summary>
    /// <remarks>
    /// POST /api/maps/routes/estimate
    /// </remarks>
    [HttpPost("routes/estimate")]
    [ProducesResponseType(typeof(RouteEstimateResult), 200)]
    public async Task<IActionResult> EstimateRouteAsync(
        [FromBody] RouteEstimateApiRequest body,
        CancellationToken cancellationToken = default)
    {
        var request = new RouteEstimateRequest
        {
            Origin = new LocationPoint(body.OriginLat, body.OriginLng),
            Destination = new LocationPoint(body.DestinationLat, body.DestinationLng),
            Provider = MapProvider.Auto,
            TravelMode = body.TravelMode ?? MapTravelMode.Car,
            IncludePolyline = true,
            RequestSource = "MapsController"
        };

        var result = await _routingService.GetRouteEstimateAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Autocomplete suggestions for a partial search query.
    /// </summary>
    /// <remarks>
    /// GET /api/maps/autocomplete?query=…&amp;lat=…&amp;lng=…
    /// </remarks>
    [HttpGet("autocomplete")]
    [ProducesResponseType(typeof(List<MapSuggestionDto>), 200)]
    public async Task<IActionResult> AutocompleteAsync(
        [FromQuery] string query,
        [FromQuery] double? lat,
        [FromQuery] double? lng,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Query is required.");

        var request = new MapAutocompleteRequest
        {
            Query = query,
            LocationLat = lat,
            LocationLng = lng
        };

        var results = await _geocodingService.AutocompleteAsync(request, cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Get full place details (including lat/lng if not present in autocomplete).
    /// </summary>
    [HttpGet("place-detail")]
    [ProducesResponseType(typeof(MapPlaceDto), 200)]
    public async Task<IActionResult> PlaceAsync(
        [FromQuery] string providerPlaceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerPlaceId))
            return BadRequest("ProviderPlaceId is required.");

        var result = await _geocodingService.GetPlaceDetailAsync(providerPlaceId, cancellationToken);
        if (result == null) return NotFound();

        return Ok(result);
    }

    /// <summary>
    /// Geocode an address query directly.
    /// </summary>
    [HttpGet("geocode")]
    [ProducesResponseType(typeof(List<MapPlaceDto>), 200)]
    public async Task<IActionResult> GeocodeAsync(
        [FromQuery] string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return BadRequest("Query is required.");

        var request = new MapGeocodeRequest { Query = query };
        var results = await _geocodingService.GeocodeAsync(request, cancellationToken);

        return Ok(results);
    }

    /// <summary>
    /// Reverse geocode coordinates to an address.
    /// </summary>
    [HttpGet("reverse")]
    [ProducesResponseType(typeof(MapPlaceDto), 200)]
    public async Task<IActionResult> ReverseGeocodeAsync(
        [FromQuery] double lat,
        [FromQuery] double lng,
        CancellationToken cancellationToken = default)
    {
        var result = await _geocodingService.ReverseGeocodeAsync(lat, lng, cancellationToken);
        if (result == null) return NotFound();

        return Ok(result);
    }
}

/// <summary>Request body for POST /api/maps/routes/estimate</summary>
public sealed class RouteEstimateApiRequest
{
    public double OriginLat { get; init; }
    public double OriginLng { get; init; }
    public double DestinationLat { get; init; }
    public double DestinationLng { get; init; }

    /// <summary>Optional. Defaults to Car if not provided.</summary>
    public MapTravelMode? TravelMode { get; init; }
}
