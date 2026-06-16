namespace SafeRide.Application.Features.Auth.DTOs;

public sealed class LinkedAccountsResponse
{
    public bool PhoneLinked { get; set; }
    public string? PhoneNumber { get; set; }
    public bool GoogleLinked { get; set; }
    public string? GoogleEmail { get; set; }
}
