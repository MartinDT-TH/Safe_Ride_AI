using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.AdminNotifications.Commands.CreateAdminNotification;

public sealed class CreateAdminNotificationCommandHandler
    : IRequestHandler<CreateAdminNotificationCommand, AdminNotificationResponse>
{
    private readonly IAdminNotificationManagementService _notificationManagementService;

    public CreateAdminNotificationCommandHandler(
        IAdminNotificationManagementService notificationManagementService)
    {
        _notificationManagementService = notificationManagementService;
    }

    public Task<AdminNotificationResponse> Handle(
        CreateAdminNotificationCommand request,
        CancellationToken cancellationToken)
    {
        return _notificationManagementService.CreateNotificationAsync(
            request.CreatedBy,
            request.Title,
            request.Content,
            request.NotificationType,
            request.TargetAudience,
            cancellationToken);
    }
}
