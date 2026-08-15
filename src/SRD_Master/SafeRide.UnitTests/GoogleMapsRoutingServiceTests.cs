using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Exceptions;
using SafeRide.Application.Common.Models;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.ExternalServices.GoogleMaps;

namespace SafeRide.UnitTests;

public sealed class GoogleMapsRoutingServiceTests
{
    private const string SuccessJson =
        """
        {
          "routes": [
            {
              "distanceMeters": 2450,
              "duration": "351s",
              "polyline": { "encodedPolyline": "encoded-test-polyline" }
            }
          ]
        }
        """;

    [Fact]
    public async Task GetRouteEstimateAsync_ValidResponse_MapsResult()
    {
        var handler = RespondWith(HttpStatusCode.OK, SuccessJson);
        var service = CreateService(handler);

        var result = await service.GetRouteEstimateAsync(
            CreateRequest(MapTravelMode.Car),
            CancellationToken.None);

        Assert.Equal(MapProvider.GoogleMaps, result.Provider);
        Assert.Equal(2450, result.DistanceMeters);
        Assert.Equal(351, result.DurationSeconds);
        Assert.Equal("encoded-test-polyline", result.EncodedPolyline);
        Assert.Equal("polyline5", result.PolylineFormat);
    }

    [Fact]
    public async Task GetRouteEstimateAsync_FractionalDuration_PreservesFraction()
    {
        var handler = RespondWith(
            HttpStatusCode.OK,
            SuccessJson.Replace("351s", "351.25s", StringComparison.Ordinal));
        var service = CreateService(handler);

        var result = await service.GetRouteEstimateAsync(
            CreateRequest(MapTravelMode.Car),
            CancellationToken.None);

        Assert.Equal(351.25, result.DurationSeconds);
    }

    [Theory]
    [InlineData("""{"routes":[]}""")]
    [InlineData("""{}""")]
    [InlineData("not-json")]
    [InlineData("""{"routes":[{"distanceMeters":2450,"duration":"abc","polyline":{"encodedPolyline":"encoded"}}]}""")]
    [InlineData("""{"routes":[{"distanceMeters":2450,"duration":null,"polyline":{"encodedPolyline":"encoded"}}]}""")]
    [InlineData("""{"routes":[{"distanceMeters":2450,"duration":"","polyline":{"encodedPolyline":"encoded"}}]}""")]
    [InlineData("""{"routes":[{"distanceMeters":2450,"duration":"351","polyline":{"encodedPolyline":"encoded"}}]}""")]
    [InlineData("""{"routes":[{"distanceMeters":2450,"duration":"0s","polyline":{"encodedPolyline":"encoded"}}]}""")]
    [InlineData("""{"routes":[{"distanceMeters":2450,"duration":"NaNs","polyline":{"encodedPolyline":"encoded"}}]}""")]
    [InlineData("""{"routes":[{"distanceMeters":2450,"duration":"Infinitys","polyline":{"encodedPolyline":"encoded"}}]}""")]
    [InlineData("""{"routes":[{"distanceMeters":2450,"duration":"351s"}]}""")]
    [InlineData("""{"routes":[{"distanceMeters":0,"duration":"351s","polyline":{"encodedPolyline":"encoded"}}]}""")]
    [InlineData("")]
    public async Task GetRouteEstimateAsync_InvalidSuccessResponse_Throws(string responseBody)
    {
        var service = CreateService(RespondWith(HttpStatusCode.OK, responseBody));

        await Assert.ThrowsAsync<MapServiceException>(
            () => service.GetRouteEstimateAsync(
                CreateRequest(MapTravelMode.Car),
                CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task GetRouteEstimateAsync_HttpError_Throws(HttpStatusCode statusCode)
    {
        var service = CreateService(RespondWith(statusCode, """{"error":"provider failure"}"""));

        await Assert.ThrowsAsync<MapServiceException>(
            () => service.GetRouteEstimateAsync(
                CreateRequest(MapTravelMode.Car),
                CancellationToken.None));
    }

    [Fact]
    public async Task GetRouteEstimateAsync_Timeout_ThrowsMapServiceException()
    {
        var handler = new RecordingHandler((_, _) =>
            throw new TaskCanceledException("Simulated HttpClient timeout."));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<MapServiceException>(
            () => service.GetRouteEstimateAsync(
                CreateRequest(MapTravelMode.Car),
                CancellationToken.None));

        Assert.Contains("quá thời gian", exception.Message);
    }

    [Fact]
    public async Task GetRouteEstimateAsync_HttpRequestException_ThrowsMapServiceException()
    {
        var handler = new RecordingHandler((_, _) =>
            throw new HttpRequestException("Simulated network failure."));
        var service = CreateService(handler);

        await Assert.ThrowsAsync<MapServiceException>(
            () => service.GetRouteEstimateAsync(
                CreateRequest(MapTravelMode.Car),
                CancellationToken.None));
    }

    [Fact]
    public async Task GetRouteEstimateAsync_CallerCancellation_IsRethrown()
    {
        var handler = new RecordingHandler((_, cancellationToken) =>
            throw new OperationCanceledException(cancellationToken));
        var service = CreateService(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetRouteEstimateAsync(
                CreateRequest(MapTravelMode.Car),
                cancellation.Token));
    }

    [Theory]
    [InlineData(MapTravelMode.Car, "DRIVE", true)]
    [InlineData(MapTravelMode.Motorcycle, "TWO_WHEELER", true)]
    [InlineData(MapTravelMode.Bike, "BICYCLE", false)]
    [InlineData(MapTravelMode.Foot, "WALK", false)]
    public async Task GetRouteEstimateAsync_SendsExpectedGoogleRequest(
        MapTravelMode mode,
        string googleMode,
        bool expectsTrafficAware)
    {
        var handler = RespondWith(HttpStatusCode.OK, SuccessJson);
        var service = CreateService(handler);

        await service.GetRouteEstimateAsync(CreateRequest(mode), CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal(
            "https://routes.googleapis.com/v2:computeRoutes",
            handler.RequestUri?.ToString());
        Assert.DoesNotContain("key=", handler.RequestUri?.Query ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("test-api-key", handler.ApiKey);
        Assert.Equal(
            "routes.distanceMeters,routes.duration,routes.polyline.encodedPolyline",
            handler.FieldMask);

        using var body = JsonDocument.Parse(handler.Body!);
        Assert.Equal(googleMode, body.RootElement.GetProperty("travelMode").GetString());
        Assert.Equal("vi-VN", body.RootElement.GetProperty("languageCode").GetString());
        Assert.Equal(
            expectsTrafficAware,
            body.RootElement.TryGetProperty("routingPreference", out var preference));
        if (expectsTrafficAware)
            Assert.Equal("TRAFFIC_AWARE", preference.GetString());
    }

    private static GoogleMapsRoutingService CreateService(RecordingHandler handler)
    {
        var client = new HttpClient(handler);
        var options = Options.Create(new GoogleMapsOptions
        {
            ApiKey = "test-api-key",
            RoutesApiUrl = "https://routes.googleapis.com/v2:computeRoutes",
            TimeoutSeconds = 15
        });
        return new GoogleMapsRoutingService(
            client,
            options,
            NullLogger<GoogleMapsRoutingService>.Instance);
    }

    private static RouteEstimateRequest CreateRequest(MapTravelMode mode)
    {
        return new RouteEstimateRequest
        {
            Origin = new LocationPoint(16.047079, 108.206230),
            Destination = new LocationPoint(16.068, 108.212),
            TravelMode = mode,
            Language = "vi",
            RequestSource = "UnitTest"
        };
    }

    private static RecordingHandler RespondWith(HttpStatusCode statusCode, string body)
    {
        return new RecordingHandler((_, _) => Task.FromResult(
            new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            }));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _response;

        public RecordingHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response)
        {
            _response = response;
        }

        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? ApiKey { get; private set; }
        public string? FieldMask { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.GetValues("X-Goog-Api-Key").Single();
            FieldMask = request.Headers.GetValues("X-Goog-FieldMask").Single();
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return await _response(request, cancellationToken);
        }
    }
}
