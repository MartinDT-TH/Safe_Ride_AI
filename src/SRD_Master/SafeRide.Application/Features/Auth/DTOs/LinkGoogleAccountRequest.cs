using System.ComponentModel.DataAnnotations;

namespace SafeRide.Application.Features.Auth.DTOs;

public sealed class LinkGoogleAccountRequest
{
    [Required]
    public string GoogleIdToken { get; set; } = string.Empty;
}
