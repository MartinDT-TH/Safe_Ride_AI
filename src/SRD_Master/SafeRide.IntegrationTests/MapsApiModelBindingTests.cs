using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Domain.Enums;

namespace SafeRide.IntegrationTests;

public sealed class MapsApiModelBindingTests
{
    [Theory]
    [InlineData("Car")]
    [InlineData("Driving")]
    [InlineData("Walking")]
    public async Task EstimateRoute_SupportedTravelMode_ReturnsOk(string travelMode)
    {
        using var baseFactory = new AuthApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IMapRoutingService>();
                services.AddSingleton<IMapRoutingService, FakeMapRoutingService>();
            }));
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/maps/routes/estimate",
            CreatePayload(travelMode));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("\"Plane\"")]
    [InlineData("1")]
    public async Task EstimateRoute_InvalidTravelMode_ReturnsValidationProblem(
        string travelModeJson)
    {
        using var baseFactory = new AuthApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IMapRoutingService>();
                services.AddSingleton<IMapRoutingService, FakeMapRoutingService>();
            }));
        using var client = factory.CreateClient();
        using var content = new StringContent(
            $$"""
            {
              "originLat": 16.047079,
              "originLng": 108.206230,
              "destinationLat": 16.068,
              "destinationLng": 108.212,
              "travelMode": {{travelModeJson}}
            }
            """,
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/api/maps/routes/estimate", content);
        var problem = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("request.validation_failed", problem);
        Assert.Contains("travelMode", problem);
        Assert.Contains("Car", problem);
        Assert.Contains("Walking", problem);
        Assert.DoesNotContain("The body field is required.", problem);
    }

    private static object CreatePayload(string travelMode)
    {
        return new
        {
            originLat = 16.047079,
            originLng = 108.206230,
            destinationLat = 16.068,
            destinationLng = 108.212,
            travelMode
        };
    }

    private sealed class FakeMapRoutingService : IMapRoutingService
    {
        public Task<RouteEstimateResult> GetRouteEstimateAsync(
            RouteEstimateRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new RouteEstimateResult
            {
                Provider = MapProvider.GoogleMaps,
                DistanceMeters = 2450,
                DurationSeconds = 351,
                EncodedPolyline = "encoded-test-polyline"
            });
        }
    }
}
