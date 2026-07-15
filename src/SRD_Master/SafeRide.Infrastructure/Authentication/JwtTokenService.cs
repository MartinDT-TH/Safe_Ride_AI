using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SafeRide.Application.Features.Auth.Services;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Persistence;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SafeRide.Infrastructure.Authentication;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly ApplicationDbContext _dbContext;

    public JwtTokenService(
        IOptions<JwtOptions> options,
        ApplicationDbContext dbContext)
    {
        _options = options.Value;
        _dbContext = dbContext;
    }

    public Task<(string Token, string JwtId, int ExpiresIn)> GenerateAccessTokenAsync(
        AspNetUser user,
        IList<string> roles)
    {
        var jwtId = Guid.NewGuid().ToString("N");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, jwtId),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.MobilePhone, user.PhoneNumber ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FullName ?? user.UserName ?? user.Id.ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var accessTokenLifetime = roles.Contains("Admin", StringComparer.OrdinalIgnoreCase)
            ? TimeSpan.FromDays(_options.AdminAccessTokenDays)
            : TimeSpan.FromMinutes(_options.AccessTokenMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(accessTokenLifetime),
            signingCredentials: credentials);

        return Task.FromResult((
            new JwtSecurityTokenHandler().WriteToken(token),
            jwtId,
            checked((int)accessTokenLifetime.TotalSeconds)));
    }

    public string GenerateRawRefreshToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    }

    public async Task<string> GenerateRefreshTokenAsync(
        Guid userId,
        string? deviceId,
        string? deviceName)
    {
        var rawToken = GenerateRawRefreshToken();
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SessionId = Guid.NewGuid(),
            TokenHash = HashToken(rawToken),
            DeviceId = deviceId,
            DeviceName = deviceName,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays)
        };

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync();
        return rawToken;
    }

    public byte[] HashToken(string token)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }
}
