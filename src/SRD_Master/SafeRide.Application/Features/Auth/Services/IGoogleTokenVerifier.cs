namespace SafeRide.Application.Features.Auth.Services;

public interface IGoogleTokenVerifier
{
    Task<GoogleUserInfo> VerifyAsync(string googleIdToken);
}

// public class GoogleUserInfo
// {
//     public string GoogleSubject { get; set; } = string.Empty;
//     public string? Email { get; set; }
//     public string? Name { get; set; }
//     public string? Picture { get; set; }
// }
public sealed record GoogleUserInfo(
    string Subject,
    string Email,
    bool EmailVerified,
    string? Name,
    string? Picture);