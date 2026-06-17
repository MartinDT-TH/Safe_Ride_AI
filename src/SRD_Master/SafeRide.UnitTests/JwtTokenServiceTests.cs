using Microsoft.Extensions.Options;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Authentication;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SafeRide.UnitTests;

public sealed class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "SafeRide.Tests",
        Audience = "SafeRide.Tests.Client",
        SecretKey = "unit-test-secret-key-that-is-long-enough-123456",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 30
    };

    [Fact]
    public async Task AccessToken_ContainsIdentityAndRoleClaims()
    {
        var user = new AspNetUser
        {
            Id = Guid.NewGuid(),
            UserName = "customer@example.test",
            Email = "customer@example.test",
            FullName = "Safe Ride Customer",
            IsActive = true
        };
        var service = new JwtTokenService(
            Microsoft.Extensions.Options.Options.Create(Options),
            null!);

        var result = await service.GenerateAccessTokenAsync(user, new[] { "Customer" });
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);

        Assert.Equal(Options.Issuer, token.Issuer);
        Assert.Equal(user.Id.ToString(), token.Subject);
        Assert.Contains(token.Claims, x => x.Type == ClaimTypes.Role && x.Value == "Customer");
        Assert.Equal(Options.AccessTokenMinutes * 60, result.ExpiresIn);
    }

    [Fact]
    public void RefreshTokens_AreRandomAndHashDeterministically()
    {
        var service = new JwtTokenService(
            Microsoft.Extensions.Options.Options.Create(Options),
            null!);
        var first = service.GenerateRawRefreshToken();
        var second = service.GenerateRawRefreshToken();

        Assert.NotEqual(first, second);
        Assert.Equal(service.HashToken(first), service.HashToken(first));
        Assert.NotEqual(service.HashToken(first), service.HashToken(second));
        Assert.Equal(32, service.HashToken(first).Length);
    }
}