using System.ComponentModel.DataAnnotations;
namespace SafeRide.Application.Features.Auth.DTOs;

public class GoogleLoginRequest
{
    [Required]
    public string GoogleIdToken { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? DeviceId { get; set; }

    [MaxLength(200)]
    public string? DeviceName { get; set; }
}
