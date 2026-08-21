using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Infrastructure.Services;

namespace SafeRide.IntegrationTests;

internal static class TestEvidenceValidation
{
    public static IEvidenceFileValidator Create(
        IFileSafetyScanner? scanner = null,
        string environmentName = "Testing") =>
        new EvidenceFileValidator(
            scanner ?? new CleanFileSafetyScanner(),
            CreateEnvironment(environmentName));

    public static IHostEnvironment CreateEnvironment(string environmentName) =>
        new TestHostEnvironment { EnvironmentName = environmentName };

    private sealed class CleanFileSafetyScanner : IFileSafetyScanner
    {
        public Task<FileSafetyScanResult> ScanAsync(
            string fileName,
            string contentType,
            Stream content,
            CancellationToken cancellationToken) =>
            Task.FromResult(new FileSafetyScanResult(FileSafetyScanStatus.Clean));
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "SafeRide.IntegrationTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

internal sealed class TestFileSafetyScanner : IFileSafetyScanner
{
    private readonly FileSafetyScanStatus _status;
    private readonly bool _drainStream;

    public TestFileSafetyScanner(FileSafetyScanStatus status, bool drainStream = false)
    {
        _status = status;
        _drainStream = drainStream;
    }

    public int Calls { get; private set; }

    public async Task<FileSafetyScanResult> ScanAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        Calls++;
        if (_drainStream)
        {
            await content.CopyToAsync(Stream.Null, cancellationToken);
        }
        return new FileSafetyScanResult(_status, "test-threat");
    }
}
