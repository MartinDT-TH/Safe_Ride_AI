namespace SafeRide.Application.Features.Auth;

public static class AuthErrorCodes
{
    public const string AccountInactive = "auth.account_inactive";
    public const string AccountConflict = "auth.account_conflict";
    public const string InvalidRefreshToken = "auth.invalid_refresh_token";
    public const string RefreshTokenExpired = "auth.refresh_token_expired";
    public const string RefreshTokenReused = "auth.refresh_token_reused";
    public const string InvalidPhoneNumber = "auth.invalid_phone_number";
    public const string PhoneNumberConflict = "auth.phone_number_conflict";
    public const string PhoneVerificationRequired = "auth.phone_verification_required";
    public const string InvalidOtp = "auth.invalid_otp";
    public const string OtpExpired = "auth.otp_expired";
    public const string OtpAttemptsExceeded = "auth.otp_attempts_exceeded";
    public const string OtpSendCooldown = "auth.otp_send_cooldown";
    public const string OtpUnavailable = "auth.otp_unavailable";
    public const string InvalidGoogleToken = "auth.invalid_google_token";
    public const string ConfigurationUnavailable = "auth.configuration_unavailable";
}
