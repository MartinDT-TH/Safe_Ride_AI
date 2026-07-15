using SafeRide.Infrastructure.Services;

namespace SafeRide.UnitTests;

public sealed class TripShareTokenServiceTests
{
    [Fact]
    public void GenerateToken_CreatesUniqueBase64UrlTokensWithAtLeast32Bytes()
    {
        var tokens = Enumerable.Range(0, 100)
            .Select(_ => TripShareTokenService.GenerateToken())
            .ToArray();

        Assert.Equal(tokens.Length, tokens.Distinct().Count());
        Assert.All(tokens, token =>
        {
            Assert.Equal(43, token.Length);
            Assert.DoesNotContain("+", token);
            Assert.DoesNotContain("/", token);
            Assert.DoesNotContain("=", token);
        });
    }

    [Fact]
    public void HashToken_ReturnsDeterministicSha256WithoutRawToken()
    {
        const string rawToken = "secret-share-token";

        var first = TripShareTokenService.HashToken(rawToken);
        var second = TripShareTokenService.HashToken(rawToken);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
        Assert.DoesNotContain(rawToken, first, StringComparison.OrdinalIgnoreCase);
    }
}
