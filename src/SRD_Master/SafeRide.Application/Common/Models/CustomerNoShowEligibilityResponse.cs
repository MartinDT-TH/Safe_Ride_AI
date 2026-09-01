using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Models;

public sealed record CustomerNoShowEligibilityResponse(
    long TripId,
    bool CanReportNoShow,
    string ReasonCode,
    string ReasonMessage,
    TripStatus TripStatus,
    DateTime? ArrivedAt,
    DateTime? ArrivalLocationVerifiedAt,
    int NoShowWaitMinutes,
    DateTime? WaitSatisfiedAt,
    DateTime ServerNow,
    long? RemainingSeconds,
    bool ReminderSent,
    DateTime? ReminderSentAt,
    bool HasExistingVerifiedNoShow);
