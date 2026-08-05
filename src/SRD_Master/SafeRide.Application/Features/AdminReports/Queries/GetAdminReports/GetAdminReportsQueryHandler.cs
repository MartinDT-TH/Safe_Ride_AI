using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Reports;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.AdminReports.Queries.GetAdminReports;

public sealed class GetAdminReportsQueryHandler
    : IRequestHandler<GetAdminReportsQuery, AdminReportPagedResult>
{
    private readonly IReportRepository _reportRepository;

    public GetAdminReportsQueryHandler(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public Task<AdminReportPagedResult> Handle(
        GetAdminReportsQuery request,
        CancellationToken cancellationToken)
    {
        return _reportRepository.GetAdminReportsAsync(
            new AdminReportListFilter(
                request.Page,
                request.PageSize,
                ParseStatus(request.Status),
                request.Search),
            cancellationToken);
    }

    private static ReportStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)
            || string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Enum.TryParse<ReportStatus>(status.Trim(), true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new ReportException(
            "report.invalid_status",
            "Trạng thái báo cáo không hợp lệ.",
            400);
    }
}
