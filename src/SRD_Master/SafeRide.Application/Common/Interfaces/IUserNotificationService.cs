using SafeRide.Application.Features.Notifications;

namespace SafeRide.Application.Common.Interfaces;

public interface IUserNotificationService
{
    Task<UserNotificationsPageResponse> GetNotificationsAsync(
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<UserNotificationResponse> MarkAsReadAsync(
        Guid userId,
        long notificationId,
        CancellationToken cancellationToken);
}
