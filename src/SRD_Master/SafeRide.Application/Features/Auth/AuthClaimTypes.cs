namespace SafeRide.Application.Features.Auth;

public static class AuthClaimTypes
{
    public const string SessionMode = "session_mode";
    public const string ContinuationTripId = "continuation_trip_id";
    public const string ReloginRequiredAfterTrip = "relogin_required_after_trip";
    public const string ContinuationAbsoluteExpiresAt = "continuation_absolute_expires_at";
}
