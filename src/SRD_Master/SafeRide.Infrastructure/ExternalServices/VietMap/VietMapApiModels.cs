using System.Text.Json;
using System.Text.Json.Serialization;

namespace SafeRide.Infrastructure.ExternalServices.VietMap;

// ──────────────────────────── Route v3 ────────────────────────────

internal sealed class VietMapRouteResponse
{
    [JsonPropertyName("paths")]
    public List<VietMapRoutePath>? Paths { get; set; }
}

internal sealed class VietMapRoutePath
{
    /// <summary>Distance in metres.</summary>
    [JsonPropertyName("distance")]
    public double Distance { get; set; }

    /// <summary>
    /// Duration returned by VietMap Route v3.
    /// VietMap returns milliseconds — normalised to seconds in VietMapRoutingService.
    /// </summary>
    [JsonPropertyName("time")]
    public double Time { get; set; }

    /// <summary>Encoded polyline string (polyline5 format).</summary>
    [JsonPropertyName("points")]
    public string? Points { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("instructions")]
    public List<VietMapInstruction>? Instructions { get; set; }
}

internal sealed class VietMapInstruction
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    /// <summary>Distance in metres.</summary>
    [JsonPropertyName("distance")]
    public double Distance { get; set; }

    /// <summary>Time in milliseconds (same unit as path.time).</summary>
    [JsonPropertyName("time")]
    public double Time { get; set; }
}

// ──────────────────────────── Autocomplete v4 ────────────────────────────

internal sealed class VietMapAutocompleteResult
{
    [JsonPropertyName("ref_id")]
    public string? RefId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("display")]
    public string? Display { get; set; }

    [JsonPropertyName("hs_code")]
    public string? HsCode { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("categories")]
    public JsonElement? Categories { get; set; }

    // Lat/lng may not be present in autocomplete — only in place detail
    [JsonPropertyName("lat")]
    public double? Lat { get; set; }

    [JsonPropertyName("lng")]
    public double? Lng { get; set; }
}

// ──────────────────────────── Geocode/Search v4 ────────────────────────────

internal sealed class VietMapGeocodeResult
{
    [JsonPropertyName("ref_id")]
    public string? RefId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("display")]
    public string? Display { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }

    [JsonPropertyName("province")]
    public string? Province { get; set; }

    [JsonPropertyName("district")]
    public string? District { get; set; }

    [JsonPropertyName("ward")]
    public string? Ward { get; set; }
}

// ──────────────────────────── Place Detail v4 ────────────────────────────

internal sealed class VietMapPlaceDetailResult
{
    [JsonPropertyName("ref_id")]
    public string? RefId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("display")]
    public string? Display { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }

    [JsonPropertyName("province")]
    public string? Province { get; set; }

    [JsonPropertyName("district")]
    public string? District { get; set; }

    [JsonPropertyName("ward")]
    public string? Ward { get; set; }
}

// ──────────────────────────── Reverse v4 ────────────────────────────

internal sealed class VietMapReverseResult
{
    [JsonPropertyName("ref_id")]
    public string? RefId { get; set; }

    [JsonPropertyName("display")]
    public string? Display { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lng")]
    public double Lng { get; set; }

    [JsonPropertyName("province")]
    public string? Province { get; set; }

    [JsonPropertyName("district")]
    public string? District { get; set; }

    [JsonPropertyName("ward")]
    public string? Ward { get; set; }
}
