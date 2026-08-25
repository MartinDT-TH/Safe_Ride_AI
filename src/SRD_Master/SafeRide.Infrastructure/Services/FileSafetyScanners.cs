using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Infrastructure.Services;

/// <summary>
/// Test/development-only acceptance shim. It does not scan for malware and must
/// never be registered outside explicitly non-production environments.
/// </summary>
public sealed class NonProductionFileSafetyScanner : IFileSafetyScanner
{
    public Task<FileSafetyScanResult> ScanAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new FileSafetyScanResult(
            FileSafetyScanStatus.DevelopmentBypass));
    }
}

/// <summary>
/// Fail-closed production fallback used until a real malware scanner is wired.
/// </summary>
public sealed class UnconfiguredFileSafetyScanner : IFileSafetyScanner
{
    public Task<FileSafetyScanResult> ScanAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new FileSafetyScanResult(
            FileSafetyScanStatus.ScannerUnavailable));
    }
}
