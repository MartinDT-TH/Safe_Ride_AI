using Google.Apis.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using SafeRide.Application.Features.Auth;
using SafeRide.Application.Features.Auth.Services;

namespace SafeRide.Infrastructure.Authentication;

public sealed class GoogleTokenVerifier : IGoogleTokenVerifier
{
    private readonly GoogleAuthOptions _options;

    public GoogleTokenVerifier(IOptions<GoogleAuthOptions> options)
    {
        _options = options.Value;
    }

    public async Task<GoogleUserInfo> VerifyAsync(string idToken)
    {
        var audiences = _options.ClientIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (audiences.Length == 0)
        {
            throw new AuthException(
                AuthErrorCodes.ConfigurationUnavailable,
                "Google OAuth Client ID chưa được cấu hình.",
                StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = audiences
                });

            if (string.IsNullOrWhiteSpace(payload.Subject)
                || string.IsNullOrWhiteSpace(payload.Email)
                || payload.EmailVerified != true)
            {
                throw new InvalidJwtException("Google account email is not verified.");
            }

            return new GoogleUserInfo(
                payload.Subject,
                payload.Email,
                true,
                payload.Name,
                payload.Picture);
        }
        catch (InvalidJwtException)
        {
            throw new AuthException(
                AuthErrorCodes.InvalidGoogleToken,
                "Google ID token không hợp lệ hoặc đã hết hạn.",
                StatusCodes.Status401Unauthorized);
        }
    }
}
