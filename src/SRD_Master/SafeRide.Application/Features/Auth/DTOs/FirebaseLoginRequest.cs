
namespace SafeRide.Application.Features.Auth.DTOs;

public class FirebaseLoginRequest
{
    public string FirebaseIdToken { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
}
