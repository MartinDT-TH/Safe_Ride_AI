using System.ComponentModel.DataAnnotations;
namespace SafeRide.Application.Features.Auth.DTOs;

public class RefreshTokenRequest
{
    public string AccessToken { get; set; } = string.Empty;
    [Required]
    [MinLength(20)]
    public string RefreshToken { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? DeviceId { get; set; }
}