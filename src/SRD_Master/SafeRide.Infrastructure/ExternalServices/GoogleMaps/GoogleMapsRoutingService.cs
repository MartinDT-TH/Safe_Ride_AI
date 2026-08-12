using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Exceptions;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Domain.Enums;


namespace SafeRide.Infrastructure.ExternalServices.GoogleMaps;

public sealed class GoogleMapsRoutingService : IMapRoutingService
{
    private const string FieldMask =
        "routes.distanceMeters,routes.duration,routes.polyline.encodedPolyline";
    private const int MaxLoggedBodyLength = 3000;
    private const string SafeRouteError =
        "Không thể tính tuyến đường. Vui lòng kiểm tra lại điểm đón và điểm đến.";

    private readonly HttpClient _httpClient;
    private readonly GoogleMapsOptions _options;
    private readonly ILogger<GoogleMapsRoutingService> _logger;

    public GoogleMapsRoutingService(
        HttpClient httpClient,
        IOptions<GoogleMapsOptions> options,
        ILogger<GoogleMapsRoutingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RouteEstimateResult> GetRouteEstimateAsync(
        RouteEstimateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new MapServiceException(
                "Dịch vụ bản đồ chưa được cấu hình. Vui lòng liên hệ quản trị viên.");
        }

        if (string.IsNullOrWhiteSpace(_options.RoutesApiUrl))
        {
            throw new MapServiceException(
                "Dịch vụ bản đồ chưa được cấu hình URL tuyến đường.");
        }

        var travelMode = request.TravelMode switch
        {
            MapTravelMode.Car => "DRIVE",
            MapTravelMode.Motorcycle => "TWO_WHEELER",
            MapTravelMode.Bike => "BICYCLE",
            MapTravelMode.Foot => "WALK",
            _ => throw new MapServiceException("Phương thức di chuyển không được hỗ trợ.")
        };
        var requestBody = new
        {
            origin = CreateWaypoint(request.Origin),
            destination = CreateWaypoint(request.Destination),
            travelMode,
            routingPreference = travelMode is "DRIVE" or "TWO_WHEELER"
                ? "TRAFFIC_AWARE"
                : null,
            languageCode = NormalizeLanguageCode(request.Language),
            units = "METRIC"
        };
        // var requestUrl = $"{_options.RoutesApiUrl}?key={_options.ApiKey}";
        // using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.RoutesApiUrl)
        {
            Content = JsonContent.Create(
                requestBody,
                options: new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    DefaultIgnoreCondition =
                        System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                })
        };
        httpRequest.Headers.Add("X-Goog-Api-Key", _options.ApiKey);
        httpRequest.Headers.Add("X-Goog-FieldMask", FieldMask);
        try
        {
            using var response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Google Routes API returned {StatusCode}: {ErrorBody}. Source={Source}",
                    (int)response.StatusCode,
                    TruncateForLog(rawBody),
                    request.RequestSource);
                throw new MapServiceException(SafeRouteError);
            }

            if (string.IsNullOrWhiteSpace(rawBody))
            {
                throw InvalidResponse("empty response body", rawBody, request.RequestSource);
            }

            GoogleRoutesResponse? result;
            try
            {
                result = JsonSerializer.Deserialize<GoogleRoutesResponse>(
                    rawBody,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Google Routes API returned invalid JSON. Body={ResponseBody}. Source={Source}",
                    TruncateForLog(rawBody),
                    request.RequestSource);
                throw new MapServiceException(SafeRouteError, exception);
            }

            var route = result?.Routes?.FirstOrDefault()
                ?? throw InvalidResponse("routes missing or empty", rawBody, request.RequestSource);
            if (route.DistanceMeters <= 0)
                throw InvalidResponse("distanceMeters must be greater than zero", rawBody, request.RequestSource);
            if (!TryParseDurationSeconds(route.Duration, out var durationSeconds))
                throw InvalidResponse("invalid duration", rawBody, request.RequestSource);
            if (string.IsNullOrWhiteSpace(route.Polyline?.EncodedPolyline))
                throw InvalidResponse("missing encodedPolyline", rawBody, request.RequestSource);

            return new RouteEstimateResult
            {
                Provider = MapProvider.GoogleMaps,
                DistanceMeters = route.DistanceMeters,
                DurationSeconds = durationSeconds,
                EncodedPolyline = route.Polyline.EncodedPolyline,
                PolylineFormat = "polyline5"
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Google Routes API timed out. Source={Source}",
                request.RequestSource);
            throw new MapServiceException(
                "Dịch vụ bản đồ phản hồi quá thời gian. Vui lòng thử lại.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Could not call Google Routes API. Source={Source}", request.RequestSource);
            throw new MapServiceException(
                "Không thể tính tuyến đường. Vui lòng kiểm tra lại điểm đón và điểm đến.",
                exception);
        }
    }

    private static object CreateWaypoint(LocationPoint point)
    {
        return new
        {
            location = new
            {
                latLng = new
                {
                    latitude = point.Latitude,
                    longitude = point.Longitude
                }
            }
        };
    }

    private static bool TryParseDurationSeconds(
        string? value,
        out double durationSeconds)
    {
        durationSeconds = 0;
        if (string.IsNullOrWhiteSpace(value) || !value.EndsWith('s'))
        {
            return false;
        }

        if (!double.TryParse(
                value[..^1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var seconds)
            || seconds <= 0
            || double.IsNaN(seconds)
            || double.IsInfinity(seconds))
        {
            return false;
        }

        durationSeconds = seconds;
        return true;
    }

    private MapServiceException InvalidResponse(
        string reason,
        string rawBody,
        string? requestSource)
    {
        _logger.LogWarning(
            "Google Routes API returned an invalid success response: {Reason}. Body={ResponseBody}. Source={Source}",
            reason,
            TruncateForLog(rawBody),
            requestSource);
        return new MapServiceException(SafeRouteError);
    }

    private static string NormalizeLanguageCode(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return "vi-VN";

        var normalized = language.Trim();
        return string.Equals(normalized, "vi", StringComparison.OrdinalIgnoreCase)
            ? "vi-VN"
            : normalized;
    }

    private static string TruncateForLog(string value)
    {
        if (value.Length <= MaxLoggedBodyLength)
            return value;

        return string.Concat(value.AsSpan(0, MaxLoggedBodyLength), "…");
    }
}
