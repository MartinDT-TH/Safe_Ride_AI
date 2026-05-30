using SafeRide.Application.Features.Auth.DTOs;

namespace SafeRide.Application.Features.Auth.Services;

public interface IAuthService
{
    Task<AuthResponse> FirebaseLoginAsync(
        FirebaseLoginRequest request,
        string? ipAddress,
        string? userAgent);

    Task<AuthResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        string? ipAddress,
        string? userAgent);

    Task LogoutAsync(LogoutRequest request);
}