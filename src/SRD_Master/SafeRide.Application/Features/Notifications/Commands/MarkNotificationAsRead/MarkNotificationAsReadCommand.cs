using MediatR;

namespace SafeRide.Application.Features.Notifications.Commands.MarkNotificationAsRead;

public sealed record MarkNotificationAsReadCommand(
    Guid UserId,
    long NotificationId) : IRequest<UserNotificationResponse>;
