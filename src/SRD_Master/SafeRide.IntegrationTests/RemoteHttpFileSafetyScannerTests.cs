using System.Net;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Infrastructure.Services;

namespace SafeRide.IntegrationTests;

public sealed class RemoteHttpFileSafetyScannerTests
{
    [Fact]
    public async Task CleanResponse_ReturnsCleanAndSendsMultipartFile()
    {
        HttpRequestMessage? request = null;
        string? requestBody = null;
        var handler = new DelegateHandler(async outgoingRequest =>
        {
            request = outgoingRequest;
            requestBody = await outgoingRequest.Content!.ReadAsStringAsync();
            return JsonResponse("{\"status\":\"clean\"}");
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://scanner.internal") };
        var scanner = CreateScanner(client);

        await using var content = new MemoryStream([0xFF, 0xD8, 0xFF]);
        var result = await scanner.ScanAsync("evidence.jpg", "image/jpeg", content, CancellationToken.None);

        Assert.Equal(FileSafetyScanStatus.Clean, result.Status);
        Assert.NotNull(request);
        Assert.Equal(HttpMethod.Post, request!.Method);
        Assert.Contains("file", requestBody);
    }

    [Theory]
    [InlineData("threat_detected", FileSafetyScanStatus.ThreatDetected)]
    [InlineData("infected", FileSafetyScanStatus.ThreatDetected)]
    [InlineData("unexpected", FileSafetyScanStatus.ScannerUnavailable)]
    public async Task ResponseStatus_IsMappedFailClosed(
        string status,
        FileSafetyScanStatus expected)
    {
        using var client = new HttpClient(
            new DelegateHandler(_ => Task.FromResult(JsonResponse(
                $"{{\"status\":\"{status}\",\"threatName\":\"Eicar\"}}"))))
        { BaseAddress = new Uri("https://scanner.internal") };
        var scanner = CreateScanner(client);

        await using var content = new MemoryStream([1, 2, 3]);
        var result = await scanner.ScanAsync("evidence.jpg", "image/jpeg", content, CancellationToken.None);

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task TransportFailure_ReturnsUnavailable()
    {
        using var client = new HttpClient(new DelegateHandler(_ =>
            throw new HttpRequestException("scanner offline")))
        { BaseAddress = new Uri("https://scanner.internal") };
        var scanner = CreateScanner(client);

        await using var content = new MemoryStream([1, 2, 3]);
        var result = await scanner.ScanAsync("evidence.jpg", "image/jpeg", content, CancellationToken.None);

        Assert.Equal(FileSafetyScanStatus.ScannerUnavailable, result.Status);
    }

    private static RemoteHttpFileSafetyScanner CreateScanner(HttpClient client) =>
        new(client, Options.Create(new EvidenceFileSafetyOptions
        {
            Enabled = true,
            ScannerType = "RemoteHttp",
            BaseUrl = "https://scanner.internal",
            ApiKey = "test-key"
        }));

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
