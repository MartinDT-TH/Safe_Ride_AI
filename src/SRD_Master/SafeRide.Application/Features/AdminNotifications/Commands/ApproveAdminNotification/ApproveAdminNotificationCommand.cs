using MediatR;

namespace SafeRide.Application.Features.AdminNotifications.Commands.ApproveAdminNotification;

public sealed record ApproveAdminNotificationCommand(
    long NotificationId,
    Guid ApprovedBy) : IRequest<AdminNotificationResponse>;
