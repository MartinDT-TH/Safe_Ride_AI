using MediatR;

namespace SafeRide.Application.Features.Notifications.Queries.GetUserNotifications;

public sealed record GetUserNotificationsQuery(
    Guid UserId,
    int Page = 1,
    int PageSize = 10) : IRequest<UserNotificationsPageResponse>;
