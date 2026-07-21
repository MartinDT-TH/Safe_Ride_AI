using MediatR;

namespace SafeRide.Application.Features.AdminNotifications.Commands.RejectAdminNotification;

public sealed record RejectAdminNotificationCommand(
    long NotificationId,
    Guid RejectedBy,
    string RejectionReason) : IRequest<AdminNotificationResponse>;
