namespace SafeRide.Application.Features.Auth;

public sealed record AccessTokenContext
{
    public string SessionMode { get; init; } = AuthSessionModes.Normal;
    public long? ContinuationTripId { get; init; }
    public bool ReloginRequiredAfterTrip { get; init; }
    public DateTime? ContinuationAbsoluteExpiresAt { get; init; }
    public int? AccessTokenMinutes { get; init; }

    public static AccessTokenContext Normal { get; } = new();
}
