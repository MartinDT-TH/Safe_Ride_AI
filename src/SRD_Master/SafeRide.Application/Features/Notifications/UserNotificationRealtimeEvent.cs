namespace SafeRide.Application.Features.Notifications;

public sealed record UserNotificationRealtimeEvent(
    Guid UserId,
    long Id,
    string Title,
    string Content,
    string? NotificationType,
    string? TranslationsJson,
    DateTime SentAt);
