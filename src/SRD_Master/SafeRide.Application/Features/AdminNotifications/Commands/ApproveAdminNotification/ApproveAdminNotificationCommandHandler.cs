using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.AdminNotifications.Commands.ApproveAdminNotification;

public sealed class ApproveAdminNotificationCommandHandler
    : IRequestHandler<ApproveAdminNotificationCommand, AdminNotificationResponse>
{
    private readonly IAdminNotificationManagementService _notificationManagementService;

    public ApproveAdminNotificationCommandHandler(
        IAdminNotificationManagementService notificationManagementService)
    {
        _notificationManagementService = notificationManagementService;
    }

    public Task<AdminNotificationResponse> Handle(
        ApproveAdminNotificationCommand request,
        CancellationToken cancellationToken)
    {
        return _notificationManagementService.ApproveNotificationAsync(
            request.NotificationId,
            request.ApprovedBy,
            cancellationToken);
    }
}
