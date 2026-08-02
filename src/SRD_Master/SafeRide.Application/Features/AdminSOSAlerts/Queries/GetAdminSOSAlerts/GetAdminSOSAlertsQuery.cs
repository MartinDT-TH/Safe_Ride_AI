using MediatR;

namespace SafeRide.Application.Features.AdminSOSAlerts.Queries.GetAdminSOSAlerts;

public sealed record GetAdminSOSAlertsQuery(
    string? Status,
    int Page = 1,
    int PageSize = 10) : IRequest<AdminSOSAlertPagedResult>;
