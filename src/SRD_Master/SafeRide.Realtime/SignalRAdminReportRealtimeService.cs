using Microsoft.AspNetCore.SignalR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;

namespace SafeRide.Realtime;

public sealed class SignalRAdminReportRealtimeService
    : IAdminReportRealtimeService
{
    private readonly IHubContext<SafeRideHub> _hubContext;

    public SignalRAdminReportRealtimeService(IHubContext<SafeRideHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task PublishReportCreatedAsync(
        ReportCreatedEvent notification,
        CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients
            .Group(RealtimeGroups.AdminReports)
            .SendAsync("ReportCreated", notification, cancellationToken);
    }
}
