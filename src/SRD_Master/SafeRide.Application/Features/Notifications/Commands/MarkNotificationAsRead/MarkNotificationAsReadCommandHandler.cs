using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.Notifications.Commands.MarkNotificationAsRead;

public sealed class MarkNotificationAsReadCommandHandler
    : IRequestHandler<MarkNotificationAsReadCommand, UserNotificationResponse>
{
    private readonly IUserNotificationService _userNotificationService;

    public MarkNotificationAsReadCommandHandler(
        IUserNotificationService userNotificationService)
    {
        _userNotificationService = userNotificationService;
    }

    public Task<UserNotificationResponse> Handle(
        MarkNotificationAsReadCommand request,
        CancellationToken cancellationToken)
    {
        return _userNotificationService.MarkAsReadAsync(
            request.UserId,
            request.NotificationId,
            cancellationToken);
    }
}
