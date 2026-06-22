using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Exceptions;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Domain.Enums;


namespace SafeRide.Infrastructure.ExternalServices.VietMap;

public sealed class VietMapRoutingService : IMapRoutingService
{
    private readonly HttpClient _httpClient;
    private readonly VietMapOptions _options;
    private readonly ILogger<VietMapRoutingService> _logger;

    public VietMapRoutingService(
        HttpClient httpClient,
        IOptions<VietMapOptions> options,
        ILogger<VietMapRoutingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RouteEstimateResult> GetRouteEstimateAsync(
        RouteEstimateRequest request,
        CancellationToken cancellationToken)
    {
        ValidateConfiguration();

        var travelMode = MapTravelModeToVietMap(request.TravelMode);
        var queryParams = new Dictionary<string, string?>
        {
            ["api-version"] = "1.1",
            ["apikey"] = _options.ApiKey,
            ["point"] = $"{request.Origin.Latitude},{request.Origin.Longitude}",
            ["point"] = $"{request.Destination.Latitude},{request.Destination.Longitude}",
            ["vehicle"] = travelMode,
            ["points_encoded"] = request.PointsEncoded ? "true" : "false",
            ["instructions"] = request.IncludeInstructions ? "true" : "false",
        };

        // VietMap Route v3 accepts multiple "point" params — build manually
        var baseUrl = _options.BaseUrl.TrimEnd('/') + _options.RouteApiPath;
        var url = $"{baseUrl}?apikey={Uri.EscapeDataString(_options.ApiKey)}" +
                  $"&point={request.Origin.Latitude},{request.Origin.Longitude}" +
                  $"&point={request.Destination.Latitude},{request.Destination.Longitude}" +
                  $"&vehicle={travelMode}" +
                  $"&points_encoded={request.PointsEncoded.ToString().ToLower()}" +
                  $"&instructions={request.IncludeInstructions.ToString().ToLower()}";

        _logger.LogDebug(
            "VietMapRoutingService calling route API. Source={Source}, Mode={Mode}",
            request.RequestSource,
            travelMode);

        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "VietMap route API failed with status {StatusCode}. Source={Source}. Body={Body}",
                    (int)response.StatusCode,
                    request.RequestSource,
                    errorBody);
                throw new MapServiceException(
                    "Không thể tính tuyến đường. Vui lòng thử lại sau.");
            }

            var result = await response.Content.ReadFromJsonAsync<VietMapRouteResponse>(
                cancellationToken: cancellationToken);

            var path = result?.Paths?.FirstOrDefault();
            if (path is null || path.Distance <= 0)
            {
                throw new MapServiceException(
                    "Không thể tính tuyến đường. Vui lòng kiểm tra lại điểm đón và điểm đến.");
            }

            // VietMap Route v3 returns time in milliseconds → convert to seconds
            var durationSeconds = NormaliseDurationToSeconds(path.Time);

            var instructions = BuildInstructions(path.Instructions);

            return new RouteEstimateResult
            {
                Provider = MapProvider.VietMap,
                DistanceMeters = path.Distance,
                DurationSeconds = durationSeconds,
                EncodedPolyline = path.Points,
                PolylineFormat = "polyline5",
                Instructions = instructions,
                Summary = path.Description,
                CalculatedAt = DateTimeOffset.UtcNow
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MapServiceException(
                "Dịch vụ bản đồ phản hồi quá thời gian. Vui lòng thử lại.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "VietMap route API network error. Source={Source}", request.RequestSource);
            throw new MapServiceException(
                "Không thể tính tuyến đường. Vui lòng kiểm tra lại điểm đón và điểm đến.", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "VietMap route API returned invalid JSON. Source={Source}", request.RequestSource);
            throw new MapServiceException(
                "Không thể tính tuyến đường. Vui lòng kiểm tra lại điểm đón và điểm đến.", ex);
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new MapServiceException(
                "Dịch vụ bản đồ chưa được cấu hình. Vui lòng liên hệ quản trị viên.");
        }

        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new MapServiceException(
                "Dịch vụ bản đồ chưa được cấu hình URL. Vui lòng liên hệ quản trị viên.");
        }
    }

    private static string MapTravelModeToVietMap(MapTravelMode mode) => mode switch
    {
        MapTravelMode.Car => "car",
        MapTravelMode.Motorcycle => "motorcycle",
        MapTravelMode.Bike => "bike",
        MapTravelMode.Foot => "foot",
        _ => "car"
    };

    /// <summary>
    /// VietMap Route v3 returns time in milliseconds.
    /// If the value looks like it's already seconds (very small for a typical trip),
    /// we accept it as-is. This guard prevents silent bugs if the API changes.
    /// </summary>
    private static double NormaliseDurationToSeconds(double rawTime)
    {
        // A trip of 0 duration is invalid
        if (rawTime <= 0) return 0;

        // Heuristic: if rawTime > 3600 and the result after /1000 is reasonable (1–86400 s),
        // assume milliseconds. Otherwise treat as seconds.
        const double maxReasonableSeconds = 86_400; // 24 hours
        if (rawTime > maxReasonableSeconds)
        {
            return rawTime / 1000d;
        }

        return rawTime;
    }

    private static IReadOnlyList<RouteInstructionDto> BuildInstructions(
        List<VietMapInstruction>? instructions)
    {
        if (instructions is null || instructions.Count == 0)
            return [];

        return instructions
            .Select((inst, i) => new RouteInstructionDto
            {
                Order = i,
                Text = inst.Text,
                DistanceMeters = inst.Distance,
                DurationSeconds = NormaliseDurationToSeconds(inst.Time)
            })
            .ToList()
            .AsReadOnly();
    }
}
