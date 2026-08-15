namespace SafeRide.Application.Features.AccountBans.DTOs;

public sealed record AccountBanConfigurationResponse(
    long Id,
    int NegativeFeedbackThreshold,
    int NegativeRatingMaxScore,
    int TemporaryBanDurationDays,
    int MaximumTemporaryBans,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    Guid? UpdatedByUserId);
