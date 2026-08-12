using MediatR;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.StaffNotifications.Commands.CreateStaffNotificationRequest;

public sealed record CreateStaffNotificationRequestCommand(
    Guid CreatedBy,
    string Title,
    string Content,
    string NotificationType,
    NotificationAudience TargetAudience) : IRequest<StaffNotificationRequestResponse>;
