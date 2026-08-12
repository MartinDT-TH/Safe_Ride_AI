using SafeRide.Application.Common.Realtime;

namespace SafeRide.Application.Common.Interfaces;

public interface IAdminReportRealtimeService
{
    Task PublishReportCreatedAsync(
        ReportCreatedEvent notification,
        CancellationToken cancellationToken = default);
}
