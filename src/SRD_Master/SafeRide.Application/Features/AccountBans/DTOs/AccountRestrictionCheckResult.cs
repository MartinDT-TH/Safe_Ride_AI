namespace SafeRide.Application.Features.AccountBans.DTOs;

public sealed record AccountRestrictionCheckResult(
    bool IsAllowed,
    string? Code,
    string? Message,
    int? RetryAfterSeconds)
{
    public static AccountRestrictionCheckResult Allowed() => new(true, null, null, null);

    public static AccountRestrictionCheckResult Denied(
        string code,
        string message,
        int? retryAfterSeconds = null)
    {
        return new AccountRestrictionCheckResult(
            false,
            code,
            message,
            retryAfterSeconds);
    }
}
