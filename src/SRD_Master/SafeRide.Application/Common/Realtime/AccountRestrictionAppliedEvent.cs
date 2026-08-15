namespace SafeRide.Application.Common.Realtime;

public sealed record AccountRestrictionAppliedEvent(
    Guid UserId,
    string BanType,
    string Reason,
    string Message,
    DateTime StartedAt,
    DateTime? EndsAt,
    int? RetryAfterSeconds,
    DateTime OccurredAt);
