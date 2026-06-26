namespace SafeRide.Application.Common.Models;

public sealed record BookingMatchingSnapshot(
    double? CurrentSearchRadiusKm,
    DateTime? ExpiresAt,
    int? EstimatedRemainingSeconds,
    string? MatchingMessage,
    bool IsExpanded);
