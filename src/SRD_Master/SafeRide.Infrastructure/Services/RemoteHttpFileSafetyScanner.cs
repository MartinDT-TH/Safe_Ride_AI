using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Infrastructure.Services;

public sealed class EvidenceFileSafetyOptions
{
    public const string SectionName = "EvidenceFileSafety";

    public bool Enabled { get; set; }
    public bool AllowDemo { get; set; }
    public bool AllowPublicDemo { get; set; }
    public string ScannerType { get; set; } = "RemoteHttp";
    public string BaseUrl { get; set; } = string.Empty;
    public string EndpointPath { get; set; } = "/api/file-safety/scan";
    public int TimeoutSeconds { get; set; } = 10;
    public string ApiKeyHeaderName { get; set; } = "X-Api-Key";
    public string ApiKey { get; set; } = string.Empty;
}

internal sealed class RemoteScanResponse
{
    public string? Status { get; set; }
    public string? ThreatName { get; set; }

    public FileSafetyScanResult ToScanResult() =>
        (Status ?? string.Empty).Trim().ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty) switch
        {
            "clean" => new FileSafetyScanResult(FileSafetyScanStatus.Clean),
            "threatdetected" or "infected" or "malware" or "malicious" or "malwaredetected" =>
                new FileSafetyScanResult(FileSafetyScanStatus.ThreatDetected, ThreatName),
            _ => new FileSafetyScanResult(FileSafetyScanStatus.ScannerUnavailable)
        };
}

/// PublicDemo is for non-sensitive demonstration files only and is not approved for real accident evidence.
public sealed class PublicDemoFileSafetyScanner : IFileSafetyScanner
{
    private readonly HttpClient _httpClient;
    private readonly EvidenceFileSafetyOptions _options;
    private readonly ILogger<PublicDemoFileSafetyScanner> _logger;

    public PublicDemoFileSafetyScanner(
        HttpClient httpClient,
        IOptions<EvidenceFileSafetyOptions> options,
        ILogger<PublicDemoFileSafetyScanner> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<FileSafetyScanResult> ScanAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_options.Enabled ||
            !_options.AllowPublicDemo ||
            !string.Equals(_options.ScannerType, "PublicDemo", StringComparison.OrdinalIgnoreCase) ||
            !Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out _) ||
            string.IsNullOrWhiteSpace(_options.EndpointPath))
        {
            return new FileSafetyScanResult(FileSafetyScanStatus.ScannerUnavailable);
        }

        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var extension = Path.GetExtension(fileName);
        var neutralFileName = $"demo-evidence{extension.ToLowerInvariant()}";
        form.Add(fileContent, "file", neutralFileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.EndpointPath)
        {
            Content = form
        };
        if (!string.IsNullOrWhiteSpace(_options.ApiKeyHeaderName) &&
            !string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation(_options.ApiKeyHeaderName, _options.ApiKey);
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if ((int)response.StatusCode == 429)
            {
                _logger.LogWarning("PublicDemo scanner unavailable. Reason=RateLimited");
                return new FileSafetyScanResult(FileSafetyScanStatus.ScannerUnavailable);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PublicDemo scanner unavailable. Reason=ProviderError");
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
        catch (TaskCanceledException)
        {
            _logger.LogWarning("PublicDemo scanner unavailable. Reason=Timeout");
            return new FileSafetyScanResult(FileSafetyScanStatus.ScannerUnavailable);
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("PublicDemo scanner unavailable. Reason=TransportError");
            return new FileSafetyScanResult(FileSafetyScanStatus.ScannerUnavailable);
        }
        catch (JsonException)
        {
            _logger.LogWarning("PublicDemo scanner unavailable. Reason=MalformedResponse");
            return new FileSafetyScanResult(FileSafetyScanStatus.ScannerUnavailable);
        }
    }
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

}
