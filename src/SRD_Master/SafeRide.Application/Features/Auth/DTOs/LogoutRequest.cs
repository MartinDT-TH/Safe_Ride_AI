namespace SafeRide.Application.Features.Auth.DTOs;

public class LogoutRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}