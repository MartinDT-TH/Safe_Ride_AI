using System.ComponentModel.DataAnnotations;

namespace SafeRide.Application.Features.Auth.DTOs;

public sealed class VerifyOtpRequest
{
    [Required]
    [MaxLength(20)]
    [RegularExpression(@"^\+?[0-9\s().-]{9,20}$")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{6}$")]
    public string OtpCode { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? DeviceId { get; set; }

    [MaxLength(200)]
    public string? DeviceName { get; set; }
}
