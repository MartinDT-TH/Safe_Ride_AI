using MediatR;

namespace SafeRide.Application.Features.StaffNotifications.Queries.GetStaffNotificationRequests;

public sealed record GetStaffNotificationRequestsQuery(
    Guid CreatedBy,
    int Page,
    int PageSize,
    string? Search,
    string? Status,
    string? Type,
    string? Audience) : IRequest<StaffNotificationRequestPagedResult>;
