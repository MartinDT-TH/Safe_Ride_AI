using System.ComponentModel.DataAnnotations;

namespace SafeRide.Application.Features.Auth.DTOs;

public class SendOtpRequest
{
    [Required]
    [MaxLength(20)]
    [RegularExpression(@"^\+?[0-9\s().-]{9,20}$")]
    public string PhoneNumber { get; set; } = string.Empty;
}
