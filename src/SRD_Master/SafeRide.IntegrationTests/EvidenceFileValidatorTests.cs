using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SafeRide.Infrastructure;
using SafeRide.Infrastructure.Services;

namespace SafeRide.IntegrationTests;

public sealed class EvidenceFileValidatorTests
{
    private static readonly string[] AllowedTypes =
        ["image/jpeg", "image/png", "image/webp", "application/pdf"];

    [Fact]
    public async Task Clean_WhenScannerDrainsStream_ReturnsFullContentAtPositionZero()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 1, 2, 3, 4 };
        var scanner = new TestFileSafetyScanner(FileSafetyScanStatus.Clean, drainStream: true);
        var validator = TestEvidenceValidation.Create(scanner);

        var validated = await validator.ValidateAsync(Request(bytes), CancellationToken.None);
        await using var content = validated.Content;

        Assert.Equal(0, content.Position);
        Assert.Equal(bytes, content.ToArray());
        Assert.Equal(1, scanner.Calls);
    }

    [Fact]
    public async Task InvalidMimeAndSignature_AreRejectedBeforeScanner()
    {
        var scanner = new TestFileSafetyScanner(FileSafetyScanStatus.Clean);
        var validator = TestEvidenceValidation.Create(scanner);

        var mime = await Assert.ThrowsAsync<BookingException>(() => validator.ValidateAsync(
            Request([0xFF, 0xD8, 0xFF], "evidence.bin", "application/octet-stream"),
            CancellationToken.None));
        var signature = await Assert.ThrowsAsync<BookingException>(() => validator.ValidateAsync(
            Request([1, 2, 3, 4], "evidence.jpg", "image/jpeg"),
            CancellationToken.None));

        Assert.Equal("test.evidence_invalid", mime.Code);
        Assert.Equal("test.evidence_invalid", signature.Code);
        Assert.Equal(0, scanner.Calls);
    }

    [Theory]
    [InlineData(FileSafetyScanStatus.ThreatDetected, "test.evidence_malware_detected", 400)]
    [InlineData(FileSafetyScanStatus.ScannerUnavailable, "test.evidence_scanner_unavailable", 503)]
    [InlineData(FileSafetyScanStatus.DevelopmentBypass, "test.evidence_scanner_unavailable", 503)]
    public async Task UnsafeScannerOutcomes_FailClosed(
        FileSafetyScanStatus status,
        string expectedCode,
        int expectedStatus)
    {
        var validator = TestEvidenceValidation.Create(new TestFileSafetyScanner(status));

        var exception = await Assert.ThrowsAsync<BookingException>(() => validator.ValidateAsync(
            Request([0xFF, 0xD8, 0xFF]),
            CancellationToken.None));

        Assert.Equal(expectedCode, exception.Code);
        Assert.Equal(expectedStatus, exception.StatusCode);
    }

    [Fact]
    public async Task NonProductionBypass_IsAcceptedOnlyOutsideProduction()
    {
        var request = Request([0xFF, 0xD8, 0xFF]);
        var testing = TestEvidenceValidation.Create(new SafeRide.Infrastructure.Services.NonProductionFileSafetyScanner());
        var production = TestEvidenceValidation.Create(
            new SafeRide.Infrastructure.Services.NonProductionFileSafetyScanner(),
            "Production");

        var validated = await testing.ValidateAsync(request, CancellationToken.None);
        await validated.Content.DisposeAsync();
        var exception = await Assert.ThrowsAsync<BookingException>(() => production.ValidateAsync(
            request with { Content = new MemoryStream([0xFF, 0xD8, 0xFF]) },
            CancellationToken.None));

        Assert.Equal("test.evidence_scanner_unavailable", exception.Code);
        Assert.Equal(503, exception.StatusCode);
    }

    [Theory]
    [InlineData("Development", typeof(NonProductionFileSafetyScanner))]
    [InlineData("Testing", typeof(NonProductionFileSafetyScanner))]
    [InlineData("Production", typeof(RemoteHttpFileSafetyScanner))]
    public void InfrastructureRegistration_GuardsNonProductionScanner(
        string environmentName,
        Type expectedScannerType)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=SafeRide_Test_ScannerRegistration;Trusted_Connection=True",
                ["TripSharing:AppLinkBaseUrl"] = "https://example.test/trips"
            }).Build();

        services.AddLogging();
        services.AddInfrastructure(
            configuration,
            TestEvidenceValidation.CreateEnvironment(environmentName));
        using var provider = services.BuildServiceProvider();

        Assert.IsType(expectedScannerType, provider.GetRequiredService<IFileSafetyScanner>());
    }

    private static EvidenceFileValidationRequest Request(
        byte[] bytes,
        string fileName = "evidence.jpg",
        string contentType = "image/jpeg") => new(
            fileName,
            contentType,
            bytes.Length,
            new MemoryStream(bytes),
            AllowedTypes,
            10_000_000,
            new EvidenceFileValidationErrorCodes(
                "test.evidence_invalid",
                "test.evidence_malware_detected",
                "test.evidence_scanner_unavailable"));
}
