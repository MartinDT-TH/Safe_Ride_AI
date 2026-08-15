using MediatR;

namespace SafeRide.Application.Features.AdminSOSAlerts.Queries.GetAdminSOSAlertDetails;

public sealed record GetAdminSOSAlertDetailsQuery(long SosAlertId)
    : IRequest<AdminSOSAlertResponse?>;
