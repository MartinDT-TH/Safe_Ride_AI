using MediatR;

namespace SafeRide.Application.Features.AdminNotifications.Queries.GetAdminNotifications;

public sealed record GetAdminNotificationsQuery(
    int Page = 1,
    int PageSize = 10,
    string? Search = null,
    string? Status = null,
    string? Type = null,
    string? Audience = null) : IRequest<AdminNotificationPagedResult>;
