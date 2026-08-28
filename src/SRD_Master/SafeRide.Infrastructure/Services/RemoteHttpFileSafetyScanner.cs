using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Infrastructure.Services;

public sealed class EvidenceFileSafetyOptions
{
    public const string SectionName = "EvidenceFileSafety";

    public bool Enabled { get; set; }
    public string ScannerType { get; set; } = "RemoteHttp";
    public string BaseUrl { get; set; } = string.Empty;
    public string EndpointPath { get; set; } = "/api/file-safety/scan";
    public int TimeoutSeconds { get; set; } = 10;
    public string ApiKeyHeaderName { get; set; } = "X-Api-Key";
    public string ApiKey { get; set; } = string.Empty;
}

public sealed class RemoteHttpFileSafetyScanner : IFileSafetyScanner
{
    private readonly HttpClient _httpClient;
    private readonly EvidenceFileSafetyOptions _options;

    public RemoteHttpFileSafetyScanner(
        HttpClient httpClient,
        IOptions<EvidenceFileSafetyOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<FileSafetyScanResult> ScanAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_options.Enabled ||
            !string.Equals(_options.ScannerType, "RemoteHttp", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(_options.BaseUrl) ||
            string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new FileSafetyScanResult(FileSafetyScanStatus.ScannerUnavailable);
        }

        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent(fileName), "fileName");
        form.Add(new StringContent(contentType), "contentType");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            _options.EndpointPath)
        {
            Content = form
        };
        request.Headers.TryAddWithoutValidation(_options.ApiKeyHeaderName, _options.ApiKey);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new FileSafetyScanResult(FileSafetyScanStatus.ScannerUnavailable);
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<RemoteScanResponse>(
                responseStream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken: cancellationToken);
            return result?.ToScanResult()
                ?? new FileSafetyScanResult(FileSafetyScanStatus.ScannerUnavailable);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return new FileSafetyScanResult(FileSafetyScanStatus.ScannerUnavailable);
        }
        catch (TaskCanceledException)
        {
            return new FileSafetyScanResult(FileSafetyScanStatus.ScannerUnavailable);
        }
        catch (JsonException)
        {
            return new FileSafetyScanResult(FileSafetyScanStatus.ScannerUnavailable);
        }
    }

    private sealed class RemoteScanResponse
    {
        public string? Status { get; set; }
        public string? ThreatName { get; set; }

        public FileSafetyScanResult ToScanResult() =>
            (Status ?? string.Empty).Trim().ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty) switch
            {
                "clean" => new FileSafetyScanResult(FileSafetyScanStatus.Clean),
                "threatdetected" or "infected" or "malwaredetected" =>
                    new FileSafetyScanResult(FileSafetyScanStatus.ThreatDetected, ThreatName),
                _ => new FileSafetyScanResult(FileSafetyScanStatus.ScannerUnavailable)
            };
    }
}
