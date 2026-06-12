using System.ComponentModel.DataAnnotations;

namespace SafeRide.Application.Features.Auth.DTOs;

public class LogoutRequest
{
    [Required]
    [MinLength(20)]
    public string RefreshToken { get; set; } = string.Empty;
}