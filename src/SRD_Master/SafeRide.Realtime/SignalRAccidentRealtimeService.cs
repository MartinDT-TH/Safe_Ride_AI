using Microsoft.AspNetCore.SignalR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;

namespace SafeRide.Realtime;

public sealed class SignalRAccidentRealtimeService : IAccidentRealtimeService
{
    private readonly IHubContext<SafeRideHub> _hubContext;

    public SignalRAccidentRealtimeService(IHubContext<SafeRideHub> hubContext) =>
        _hubContext = hubContext;

    public Task PublishAccidentCreatedAsync(
        AccidentCreatedEvent notification,
        CancellationToken cancellationToken = default) =>
        _hubContext.Clients
            .Group(RealtimeGroups.ManagementAccidents)
            .SendAsync("AccidentCreated", notification, cancellationToken);
}
