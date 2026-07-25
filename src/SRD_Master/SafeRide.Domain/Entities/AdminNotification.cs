using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class AdminNotification
{
    public long Id { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string NotificationType { get; set; } = null!;

    public NotificationAudience TargetAudience { get; set; } = NotificationAudience.Both;

    public AdminNotificationStatus Status { get; set; } = AdminNotificationStatus.Pending;

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public Guid? RejectedBy { get; set; }

    public DateTime? RejectedAt { get; set; }

    public string? RejectedReason { get; set; }

    public AspNetUser CreatedByUser { get; set; } = null!;

    public AspNetUser? ApprovedByUser { get; set; }

    public AspNetUser? RejectedByUser { get; set; }
}
