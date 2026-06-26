using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Exceptions;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;

namespace SafeRide.Infrastructure.ExternalServices.GoogleMaps;

public sealed class GoogleMapsService : IGoogleMapsService
{
    private const string FieldMask =
        "routes.distanceMeters,routes.duration,routes.polyline.encodedPolyline";

    private readonly HttpClient _httpClient;
    private readonly GoogleMapsOptions _options;
    private readonly ILogger<GoogleMapsService> _logger;

    public GoogleMapsService(
        HttpClient httpClient,
        IOptions<GoogleMapsOptions> options,
        ILogger<GoogleMapsService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RouteEstimateResult> GetRouteEstimateAsync(
        LocationPoint pickup,
        LocationPoint destination,
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

        var requestBody = new
        {
            origin = CreateWaypoint(pickup),
            destination = CreateWaypoint(destination),
            travelMode = "DRIVE",
            routingPreference = "TRAFFIC_AWARE",
            languageCode = "vi-VN",
            units = "METRIC"
        };

        var requestUrl = QueryHelpers.AddQueryString(
            _options.RoutesApiUrl,
            "key",
            _options.ApiKey);

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = JsonContent.Create(requestBody)
        };
        // request.Headers.Add("X-Goog-Api-Key", _options.ApiKey);
        request.Headers.Add("X-Goog-FieldMask", FieldMask);

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Google Routes API returned {StatusCode}: {ErrorBody}",
                    (int)response.StatusCode,
                    errorBody);
                throw new MapServiceException(
                    "Không thể tính tuyến đường. Vui lòng kiểm tra lại điểm đón và điểm đến.");
            }

            var result = await response.Content.ReadFromJsonAsync<GoogleRoutesResponse>(
                cancellationToken: cancellationToken);
            var route = result?.Routes.FirstOrDefault();

            if (route is null
                || route.DistanceMeters <= 0
                || !TryParseDurationMinutes(route.Duration, out var durationMinutes)
                || string.IsNullOrWhiteSpace(route.Polyline?.EncodedPolyline))
            {
                throw new MapServiceException(
                    "Không thể tính tuyến đường. Vui lòng kiểm tra lại điểm đón và điểm đến.");
            }

            return new RouteEstimateResult
            {
                Provider = SafeRide.Domain.Enums.MapProvider.GoogleMaps,
                DistanceMeters = route.DistanceMeters,
                DurationSeconds = durationMinutes * 60d, // durationMinutes is returned from TryParse, though seconds would be better
                EncodedPolyline = route.Polyline.EncodedPolyline
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MapServiceException(
                "Dịch vụ bản đồ phản hồi quá thời gian. Vui lòng thử lại.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Could not call Google Routes API.");
            throw new MapServiceException(
                "Không thể tính tuyến đường. Vui lòng kiểm tra lại điểm đón và điểm đến.",
                exception);
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "Google Routes API returned invalid JSON.");
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

    private static bool TryParseDurationMinutes(
        string value,
        out int durationMinutes)
    {
        durationMinutes = 0;
        if (string.IsNullOrWhiteSpace(value) || !value.EndsWith('s'))
        {
            return false;
        }

        if (!double.TryParse(
                value[..^1],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var seconds)
            || seconds <= 0)
        {
            return false;
        }

        durationMinutes = Math.Max(1, (int)Math.Ceiling(seconds / 60d));
        return true;
    }
}
