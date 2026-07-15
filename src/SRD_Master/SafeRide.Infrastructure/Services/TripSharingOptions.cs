namespace SafeRide.Infrastructure.Services;

public sealed class TripSharingOptions
{
    public const string SectionName = "TripSharing";

    public string AppLinkBaseUrl { get; set; } = string.Empty;
    public int DefaultExpirationHours { get; set; } = 6;
    public int CompletedGraceMinutes { get; set; } = 15;
    public int CancelledGraceMinutes { get; set; } = 5;
}
