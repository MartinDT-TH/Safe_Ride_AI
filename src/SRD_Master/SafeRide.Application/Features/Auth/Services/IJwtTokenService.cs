using SafeRide.Application.Features.Auth;
using SafeRide.Domain.Entities;

namespace SafeRide.Application.Features.Auth.Services;

public interface IJwtTokenService
{
    Task<(string Token, string JwtId, int ExpiresIn)> GenerateAccessTokenAsync(
        AspNetUser user,
        IList<string> roles,
        AccessTokenContext? context = null);

    string GenerateRawRefreshToken();

    Task<string> GenerateRefreshTokenAsync(
        Guid userId,
        string? deviceId,
        string? deviceName);

    byte[] HashToken(string token);
}
