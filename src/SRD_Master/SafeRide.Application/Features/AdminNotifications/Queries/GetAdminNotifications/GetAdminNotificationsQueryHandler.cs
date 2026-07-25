using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.AdminNotifications.Queries.GetAdminNotifications;

public sealed class GetAdminNotificationsQueryHandler
    : IRequestHandler<GetAdminNotificationsQuery, AdminNotificationPagedResult>
{
    private readonly IAdminNotificationManagementService _notificationManagementService;

    public GetAdminNotificationsQueryHandler(
        IAdminNotificationManagementService notificationManagementService)
    {
        _notificationManagementService = notificationManagementService;
    }

    public Task<AdminNotificationPagedResult> Handle(
        GetAdminNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        return _notificationManagementService.GetNotificationsAsync(
            new AdminNotificationListFilter(
                request.Page,
                request.PageSize,
                request.Search,
                request.Status,
                request.Type,
                request.Audience),
            cancellationToken);
    }
}
