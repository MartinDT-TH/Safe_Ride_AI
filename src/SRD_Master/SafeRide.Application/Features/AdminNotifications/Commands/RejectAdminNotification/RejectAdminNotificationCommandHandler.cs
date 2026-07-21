using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.AdminNotifications.Commands.RejectAdminNotification;

public sealed class RejectAdminNotificationCommandHandler
    : IRequestHandler<RejectAdminNotificationCommand, AdminNotificationResponse>
{
    private readonly IAdminNotificationManagementService _notificationManagementService;

    public RejectAdminNotificationCommandHandler(
        IAdminNotificationManagementService notificationManagementService)
    {
        _notificationManagementService = notificationManagementService;
    }

    public Task<AdminNotificationResponse> Handle(
        RejectAdminNotificationCommand request,
        CancellationToken cancellationToken)
    {
        return _notificationManagementService.RejectNotificationAsync(
            request.NotificationId,
            request.RejectedBy,
            request.RejectionReason,
            cancellationToken);
    }
}
