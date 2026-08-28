using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Infrastructure.Services;

namespace SafeRide.IntegrationTests;

public sealed class PublicDemoFileSafetyScannerTests
{
    [Fact]
    public async Task CleanResponse_UsesNeutralFilenameAndReturnsClean()
    {
        string? body = null;
        using var client = CreateClient(async request =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return JsonResponse("{\"status\":\"clean\"}");
        });
        var scanner = CreateScanner(client);

        await using var content = new MemoryStream([0xFF, 0xD8, 0xFF]);
        var result = await scanner.ScanAsync(
            "NguyenVanA_accident_2026.jpg", "image/jpeg", content, CancellationToken.None);

        Assert.Equal(FileSafetyScanStatus.Clean, result.Status);
        Assert.Contains("demo-evidence.jpg", body);
        Assert.DoesNotContain("NguyenVanA_accident_2026.jpg", body);
        Assert.DoesNotContain("AccidentId", body);
        Assert.DoesNotContain("TripId", body);
        Assert.DoesNotContain("CustomerId", body);
        Assert.DoesNotContain("DriverId", body);
    }

    [Theory]
    [InlineData("malicious", FileSafetyScanStatus.ThreatDetected)]
    [InlineData("infected", FileSafetyScanStatus.ThreatDetected)]
    [InlineData("threat_detected", FileSafetyScanStatus.ThreatDetected)]
    [InlineData("unknown", FileSafetyScanStatus.ScannerUnavailable)]
    public async Task Results_MapStrictlyFailClosed(
        string status,
        FileSafetyScanStatus expected)
    {
        using var client = CreateClient(_ => Task.FromResult(
            JsonResponse($"{{\"status\":\"{status}\"}}")));
        var scanner = CreateScanner(client);

        await using var content = new MemoryStream([1, 2, 3]);
        var result = await scanner.ScanAsync("sample.jpg", "image/jpeg", content, CancellationToken.None);

        Assert.Equal(expected, result.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ProviderFailures_ReturnUnavailable(HttpStatusCode statusCode)
    {
        using var client = CreateClient(_ => Task.FromResult(new HttpResponseMessage(statusCode)));
        var scanner = CreateScanner(client);

        await using var content = new MemoryStream([1, 2, 3]);
        var result = await scanner.ScanAsync("sample.jpg", "image/jpeg", content, CancellationToken.None);

        Assert.Equal(FileSafetyScanStatus.ScannerUnavailable, result.Status);
    }

    [Fact]
    public async Task Timeout_ReturnsUnavailable()
    {
        using var client = CreateClient(_ => throw new TaskCanceledException("timeout"));
        var scanner = CreateScanner(client);

        await using var content = new MemoryStream([1, 2, 3]);
        var result = await scanner.ScanAsync("sample.jpg", "image/jpeg", content, CancellationToken.None);

        Assert.Equal(FileSafetyScanStatus.ScannerUnavailable, result.Status);
    }

    [Fact]
    public async Task MalformedResponse_ReturnsUnavailable()
    {
        using var client = CreateClient(_ => Task.FromResult(JsonResponse("not-json")));
        var scanner = CreateScanner(client);

        await using var content = new MemoryStream([1, 2, 3]);
        var result = await scanner.ScanAsync("sample.jpg", "image/jpeg", content, CancellationToken.None);

        Assert.Equal(FileSafetyScanStatus.ScannerUnavailable, result.Status);
    }

    [Fact]
    public async Task InvalidInput_IsRejectedBeforeProviderCall()
    {
        var calls = 0;
        using var client = CreateClient(_ =>
        {
            calls++;
            return Task.FromResult(JsonResponse("{\"status\":\"clean\"}"));
        });
        var scanner = CreateScanner(client);
        var validator = TestEvidenceValidation.Create(scanner);

        var exception = await Assert.ThrowsAsync<BookingException>(() => validator.ValidateAsync(
            new EvidenceFileValidationRequest(
                "sample.jpg", "image/jpeg", 3, new MemoryStream([1, 2, 3]),
                ["image/jpeg"], 10_000,
                new EvidenceFileValidationErrorCodes("invalid", "malware", "unavailable")),
            CancellationToken.None));

        Assert.Equal("invalid", exception.Code);
        Assert.Equal(0, calls);
    }

    private static PublicDemoFileSafetyScanner CreateScanner(HttpClient client) =>
        new(
            client,
            Options.Create(new EvidenceFileSafetyOptions
            {
                Enabled = true,
                AllowPublicDemo = true,
                ScannerType = "PublicDemo",
                BaseUrl = "https://public-scanner.example",
                EndpointPath = "/scan"
            }),
            NullLogger<PublicDemoFileSafetyScanner>.Instance);

    private static HttpClient CreateClient(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) =>
        new(new DelegateHandler(handler))
        {
            BaseAddress = new Uri("https://public-scanner.example")
        };

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class DelegateHandler(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => handler(request);
    }
}
