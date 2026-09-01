using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Infrastructure;
using SafeRide.Infrastructure.Services;

namespace SafeRide.IntegrationTests;

public sealed class FileSafetyScannerSelectionTests
{
    [Theory]
    [InlineData("Production", false, "RemoteHttp", false, typeof(UnconfiguredFileSafetyScanner))]
    [InlineData("Production", true, "Demo", false, typeof(UnconfiguredFileSafetyScanner))]
    [InlineData("Production", true, "Demo", true, typeof(NonProductionFileSafetyScanner))]
    [InlineData("Production", true, "PublicDemo", true, typeof(PublicDemoFileSafetyScanner))]
    [InlineData("Production", true, "RemoteHttp", false, typeof(RemoteHttpFileSafetyScanner))]
    [InlineData("Production", true, "Unsupported", true, typeof(UnconfiguredFileSafetyScanner))]
    [InlineData("Development", false, "RemoteHttp", false, typeof(NonProductionFileSafetyScanner))]
    public void Selection_IsExplicitAndFailClosed(
        string environmentName,
        bool enabled,
        string scannerType,
        bool allowDemo,
        Type expectedType)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=SafeRide_Test_ScannerSelection;Trusted_Connection=True",
                ["TripSharing:AppLinkBaseUrl"] = "https://example.test/trips",
                ["EvidenceFileSafety:Enabled"] = enabled.ToString(),
                ["EvidenceFileSafety:ScannerType"] = scannerType,
                ["EvidenceFileSafety:AllowDemo"] = allowDemo.ToString(),
                ["EvidenceFileSafety:AllowPublicDemo"] = allowDemo.ToString(),
                ["EvidenceFileSafety:BaseUrl"] = "https://scanner.example",
                ["EvidenceFileSafety:EndpointPath"] = "/scan"
            }).Build(),
            TestEvidenceValidation.CreateEnvironment(environmentName));

        using var provider = services.BuildServiceProvider();
        Assert.IsType(expectedType, provider.GetRequiredService<IFileSafetyScanner>());
    }
}
