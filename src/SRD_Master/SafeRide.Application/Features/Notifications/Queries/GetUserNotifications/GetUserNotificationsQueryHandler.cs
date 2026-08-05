using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.Notifications.Queries.GetUserNotifications;

public sealed class GetUserNotificationsQueryHandler
    : IRequestHandler<GetUserNotificationsQuery, UserNotificationsPageResponse>
{
    private readonly IUserNotificationService _userNotificationService;

    public GetUserNotificationsQueryHandler(
        IUserNotificationService userNotificationService)
    {
        _userNotificationService = userNotificationService;
    }

    public Task<UserNotificationsPageResponse> Handle(
        GetUserNotificationsQuery request,
        CancellationToken cancellationToken)
    {
        return _userNotificationService.GetNotificationsAsync(
            request.UserId,
            request.Page,
            request.PageSize,
            cancellationToken);
    }
}
