using SafeRide.Application.Features.Auth.DTOs;

namespace SafeRide.Application.Features.Auth.Services;

public interface IAuthService
{
    Task SendOtpAsync(SendOtpRequest request);

    Task SendProfilePhoneOtpAsync(Guid userId, SendOtpRequest request);

    Task<AuthResponse> VerifyOtpAsync(
        VerifyOtpRequest request,
        string? ipAddress,
        string? userAgent);

    Task VerifyProfilePhoneOtpAsync(Guid userId, VerifyOtpRequest request);


    Task<AuthResponse> GoogleLoginAsync(
        GoogleLoginRequest request,
        string? ipAddress,
        string? userAgent);

    Task<LinkedAccountsResponse> GetLinkedAccountsAsync(Guid userId);

    Task<LinkedAccountsResponse> LinkGoogleAccountAsync(
        Guid userId,
        LinkGoogleAccountRequest request);

    Task<LinkedAccountsResponse> UnlinkGoogleAccountAsync(Guid userId);

    Task<AuthResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        string? ipAddress,
        string? userAgent);

    Task LogoutAsync(LogoutRequest request);
}
