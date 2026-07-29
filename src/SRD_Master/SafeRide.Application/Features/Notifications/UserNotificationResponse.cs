namespace SafeRide.Application.Features.Notifications;

public sealed record UserNotificationResponse(
    long Id,
    string Title,
    string Content,
    string? NotificationType,
    bool IsRead,
    DateTime SentAt,
    DateTime? ReadAt);
