namespace SafeRide.Domain.Entities;

public sealed class AccountBanConfiguration
{
    public const long SingletonId = 1;

    public long Id { get; set; } = SingletonId;
    public int NegativeFeedbackThreshold { get; set; }
    public int NegativeRatingMaxScore { get; set; }
    public int TemporaryBanDurationDays { get; set; }
    public int MaximumTemporaryBans { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedByUserId { get; set; }

    public AspNetUser? UpdatedByUser { get; set; }
}
