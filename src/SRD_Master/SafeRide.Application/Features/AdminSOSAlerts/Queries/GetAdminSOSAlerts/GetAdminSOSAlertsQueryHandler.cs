using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Safety;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.AdminSOSAlerts.Queries.GetAdminSOSAlerts;

public sealed class GetAdminSOSAlertsQueryHandler
    : IRequestHandler<GetAdminSOSAlertsQuery, AdminSOSAlertPagedResult>
{
    private readonly ISOSAlertRepository _sosAlertRepository;

    public GetAdminSOSAlertsQueryHandler(ISOSAlertRepository sosAlertRepository)
    {
        _sosAlertRepository = sosAlertRepository;
    }

    public Task<AdminSOSAlertPagedResult> Handle(
        GetAdminSOSAlertsQuery request,
        CancellationToken cancellationToken)
    {
        return _sosAlertRepository.GetAdminAlertsAsync(
            ParseStatus(request.Status),
            request.Page,
            request.PageSize,
            cancellationToken);
    }

    private static SOSStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)
            || string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Enum.TryParse<SOSStatus>(status.Trim(), true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new SafetyException(
            "sos.invalid_status",
            "Trạng thái SOS không hợp lệ.",
            400);
    }
}
