using Microsoft.AspNetCore.SignalR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Notifications;

namespace SafeRide.Realtime;

public sealed class SignalRSystemNotificationDeliveryService
    : ISystemNotificationDeliveryService
{
    private const string SystemNotificationEventName = "SystemNotificationReceived";

    private readonly IHubContext<SafeRideHub> _hubContext;

    public SignalRSystemNotificationDeliveryService(
        IHubContext<SafeRideHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishUserNotificationsAsync(
        IReadOnlyCollection<UserNotificationRealtimeEvent> notifications,
        CancellationToken cancellationToken = default)
    {
        if (notifications.Count == 0)
        {
            return Task.CompletedTask;
        }

        return Task.WhenAll(notifications.Select(notification =>
            _hubContext.Clients
                .Group(RealtimeGroups.User(notification.UserId))
                .SendAsync(
                    SystemNotificationEventName,
                    notification,
                    cancellationToken)));
    }
}
