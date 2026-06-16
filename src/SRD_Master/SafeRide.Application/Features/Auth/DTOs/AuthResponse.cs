namespace SafeRide.Application.Features.Auth.DTOs;

public class AuthResponse
{
    public string TokenType { get; set; } = "Bearer";
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }

    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }

    public IList<string> Roles { get; set; } = new List<string>();
    public string NextStep { get; set; } = AuthNextSteps.CustomerHome;
}

public static class AuthNextSteps
{
    public const string CompleteProfile = "completeProfile";
    public const string CustomerHome = "customerHome";
    public const string SelectRole = "selectRole";
}
