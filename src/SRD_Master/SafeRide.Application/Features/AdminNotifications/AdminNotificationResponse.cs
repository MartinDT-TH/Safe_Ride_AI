namespace SafeRide.Application.Features.AdminNotifications;

public sealed record AdminNotificationResponse(
    long Id,
    string Title,
    string Content,
    string NotificationType,
    string TargetAudience,
    string Status,
    Guid CreatedBy,
    string CreatedByName,
    DateTime CreatedAt,
    Guid? ApprovedBy,
    string? ApprovedByName,
    DateTime? ApprovedAt,
    Guid? RejectedBy,
    string? RejectedByName,
    DateTime? RejectedAt,
    string? RejectedReason);
