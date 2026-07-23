using SafeRide.Application.Features.Notifications;

namespace SafeRide.Application.Common.Interfaces;

public interface ISystemNotificationDeliveryService
{
    Task PublishUserNotificationsAsync(
        IReadOnlyCollection<UserNotificationRealtimeEvent> notifications,
        CancellationToken cancellationToken = default);
}
