namespace SafeRide.Infrastructure.Authentication;

public sealed class TripContinuationOptions
{
    public const string SectionName = "Authentication:TripContinuation";

    public bool Enabled { get; init; } = true;
    public int ExpiredRefreshGraceMinutes { get; init; } = 60;
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenMinutes { get; init; } = 30;
    public int AbsoluteMaxHoursFromTripStart { get; init; } = 12;
    public int PostCompletionRatingGraceMinutes { get; init; } = 5;
}
