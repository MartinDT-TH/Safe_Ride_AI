namespace SafeRide.Application.Features.Notifications;

public sealed record UserNotificationsPageResponse(
    IReadOnlyList<UserNotificationResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages,
    int UnreadCount);
