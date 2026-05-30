namespace SafeRide.Application.Features.Auth.Services;

public interface IFirebaseTokenVerifier
{
    Task<FirebaseUserInfo> VerifyAsync(string firebaseIdToken);
}

public class FirebaseUserInfo
{
    public string FirebaseUid { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Name { get; set; }
    public string? Picture { get; set; }
}