using System.ComponentModel.DataAnnotations;

namespace SafeRide.Application.Features.Auth.DTOs;

public sealed class UpdateProfileRequest
{
    [Required]
    [StringLength(250, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(256)]
    public string? Email { get; set; }
}
