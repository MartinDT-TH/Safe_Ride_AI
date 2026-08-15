using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class AccountBanHistory
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public AccountBanType BanType { get; set; }
    public AccountBanSource Source { get; set; }
    public AccountBanStatus Status { get; set; } = AccountBanStatus.Active;
    public string Reason { get; set; } = string.Empty;
    public string? Trigger { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }
    public long? TriggeringRatingId { get; set; }
    public int? NegativeFeedbackCount { get; set; }
    public int? TemporaryBanSequence { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public Guid? ReleasedByUserId { get; set; }
    public string? ReleaseReason { get; set; }

    public AspNetUser User { get; set; } = null!;
    public AspNetUser? CreatedByUser { get; set; }
    public AspNetUser? ReleasedByUser { get; set; }
    public Rating? TriggeringRating { get; set; }
}
