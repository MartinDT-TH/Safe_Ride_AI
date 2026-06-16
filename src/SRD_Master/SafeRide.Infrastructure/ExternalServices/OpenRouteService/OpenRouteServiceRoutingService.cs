using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Exceptions;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;

namespace SafeRide.Infrastructure.ExternalServices.OpenRouteService;

public sealed class OpenRouteServiceRoutingService : IGoogleMapsService
{
    private readonly HttpClient _httpClient;
    private readonly OpenRouteServiceOptions _options;
    private readonly ILogger<OpenRouteServiceRoutingService> _logger;

    public OpenRouteServiceRoutingService(
        HttpClient httpClient,
        IOptions<OpenRouteServiceOptions> options,
        ILogger<OpenRouteServiceRoutingService> logger)
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
        ValidateConfiguration();

        try
        {
            var matrix = await GetMatrixAsync(pickup, destination, cancellationToken);
            var encodedPolyline = await GetEncodedPolylineAsync(
                pickup,
                destination,
                cancellationToken);

            return new RouteEstimateResult(
                Math.Round(matrix.DistanceMeters / 1000d, 2, MidpointRounding.AwayFromZero),
                Math.Max(1, (int)Math.Ceiling(matrix.DurationSeconds / 60d)),
                encodedPolyline);
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "OpenRouteService returned invalid JSON.");
            throw new MapServiceException(
                "Không thể tính tuyến đường. Vui lòng kiểm tra lại điểm đón và điểm đến.",
                exception);
        }
    }

    private async Task<(double DistanceMeters, double DurationSeconds)> GetMatrixAsync(
        LocationPoint pickup,
        LocationPoint destination,
        CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            locations = new[]
            {
                CreateCoordinate(pickup),
                CreateCoordinate(destination)
            },
            sources = new[] { "0" },
            destinations = new[] { "1" },
            metrics = new[] { "distance", "duration" },
            units = "m"
        };

        using var request = CreatePostRequest(_options.MatrixApiUrl, requestBody);
        using var response = await SendAsync(request, "OpenRouteService Matrix API", cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<OpenRouteMatrixResponse>(
            cancellationToken: cancellationToken);

        var distanceMeters = result?.Distances?.FirstOrDefault()?.FirstOrDefault();
        var durationSeconds = result?.Durations?.FirstOrDefault()?.FirstOrDefault();
        if (!distanceMeters.HasValue
            || !durationSeconds.HasValue
            || distanceMeters.Value <= 0
            || durationSeconds.Value <= 0)
        {
            throw new MapServiceException(
                "Không thể tính khoảng cách và thời gian. Vui lòng kiểm tra lại điểm đón và điểm đến.");
        }

        return (distanceMeters.Value, durationSeconds.Value);
    }

    private async Task<string> GetEncodedPolylineAsync(
        LocationPoint pickup,
        LocationPoint destination,
        CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            coordinates = new[]
            {
                CreateCoordinate(pickup),
                CreateCoordinate(destination)
            },
            geometry = true,
            instructions = false,
            preference = "recommended"
        };

        using var request = CreatePostRequest(_options.DirectionsApiUrl, requestBody);
        using var response = await SendAsync(request, "OpenRouteService Directions API", cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<OpenRouteDirectionsResponse>(
            cancellationToken: cancellationToken);
        var encodedPolyline = result?.Routes.FirstOrDefault()?.Geometry;

        if (string.IsNullOrWhiteSpace(encodedPolyline))
        {
            throw new MapServiceException(
                "Không thể tính tuyến đường. Vui lòng kiểm tra lại điểm đón và điểm đến.");
        }

        return encodedPolyline;
    }

    private HttpRequestMessage CreatePostRequest(string url, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation("Authorization", _options.ApiKey);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        string apiName,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            var statusCode = (int)response.StatusCode;
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Dispose();
            _logger.LogWarning(
                "{ApiName} returned {StatusCode}: {ErrorBody}",
                apiName,
                statusCode,
                errorBody);
            throw new MapServiceException(
                "Không thể tính tuyến đường. Vui lòng kiểm tra lại cấu hình bản đồ hoặc điểm đón và điểm đến.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MapServiceException(
                "Dịch vụ bản đồ phản hồi quá thời gian. Vui lòng thử lại.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Could not call {ApiName}.", apiName);
            throw new MapServiceException(
                "Không thể tính tuyến đường. Vui lòng kiểm tra lại điểm đón và điểm đến.",
                exception);
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "{ApiName} returned invalid JSON.", apiName);
            throw new MapServiceException(
                "Không thể tính tuyến đường. Vui lòng kiểm tra lại điểm đón và điểm đến.",
                exception);
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new MapServiceException(
                "Dịch vụ OpenRouteService chưa được cấu hình API key.");
        }

        if (string.IsNullOrWhiteSpace(_options.DirectionsApiUrl)
            || string.IsNullOrWhiteSpace(_options.MatrixApiUrl))
        {
            throw new MapServiceException(
                "Dịch vụ OpenRouteService chưa được cấu hình URL.");
        }
    }

    private static double[] CreateCoordinate(LocationPoint point)
    {
        return [point.Longitude, point.Latitude];
    }
}
