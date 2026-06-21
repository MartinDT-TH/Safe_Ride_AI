namespace SafeRide.Application.Common.Models;

public sealed class MatchingOptions
{
    public const string SectionName = "MatchingOptions";

    public double InitialRadiusKm { get; set; } = 5;

    public double ExpandedRadiusKm { get; set; } = 10;

    public int ExpandAfterMinutes { get; set; } = 3;

    public int BookingExpireAfterMinutes { get; set; } = 10;

    public int OfferExpireSeconds { get; set; } = 30;

    public int CustomerConfirmExpireSeconds { get; set; } = 90;

    public int MatchingTickSeconds { get; set; } = 10;

    public bool MockDriverAutoProgressAfterConfirm { get; set; } = true;

    public bool MockDriverAutoCompleteTrips { get; set; } = true;

    public int MockDriverTtlRefreshSeconds { get; set; } = 60;
}
