using MediatR;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.AdminNotifications.Commands.CreateAdminNotification;

public sealed record CreateAdminNotificationCommand(
    Guid CreatedBy,
    string Title,
    string Content,
    string NotificationType,
    NotificationAudience TargetAudience) : IRequest<AdminNotificationResponse>;
