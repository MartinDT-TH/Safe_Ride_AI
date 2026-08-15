using Microsoft.AspNetCore.SignalR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;

namespace SafeRide.Realtime;

public sealed class SignalRAccountRestrictionRealtimeService
    : IAccountRestrictionRealtimeService
{
    private const string AccountRestrictionAppliedEventName = "AccountRestrictionApplied";

    private readonly IHubContext<SafeRideHub> _hubContext;

    public SignalRAccountRestrictionRealtimeService(
        IHubContext<SafeRideHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishAccountRestrictionAppliedAsync(
        AccountRestrictionAppliedEvent notification,
        CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients
            .Group(RealtimeGroups.User(notification.UserId))
            .SendAsync(
                AccountRestrictionAppliedEventName,
                notification,
                cancellationToken);
    }
}
