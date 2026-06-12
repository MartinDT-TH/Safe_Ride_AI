using System.ComponentModel.DataAnnotations;

namespace SafeRide.Application.Features.Auth.DTOs;

public class SendOtpRequest
{
    [Required]
    [Phone]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;
}