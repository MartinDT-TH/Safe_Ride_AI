using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.AdminSOSAlerts.Queries.GetAdminSOSAlertDetails;

public sealed class GetAdminSOSAlertDetailsQueryHandler
    : IRequestHandler<GetAdminSOSAlertDetailsQuery, AdminSOSAlertResponse?>
{
    private readonly ISOSAlertRepository _sosAlertRepository;

    public GetAdminSOSAlertDetailsQueryHandler(ISOSAlertRepository sosAlertRepository)
    {
        _sosAlertRepository = sosAlertRepository;
    }

    public Task<AdminSOSAlertResponse?> Handle(
        GetAdminSOSAlertDetailsQuery request,
        CancellationToken cancellationToken)
    {
        return _sosAlertRepository.GetAdminAlertByIdAsync(
            request.SosAlertId,
            cancellationToken);
    }
}
