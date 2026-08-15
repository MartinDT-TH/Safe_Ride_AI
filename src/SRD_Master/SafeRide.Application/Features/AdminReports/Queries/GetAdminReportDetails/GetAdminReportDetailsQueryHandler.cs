using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Reports;

namespace SafeRide.Application.Features.AdminReports.Queries.GetAdminReportDetails;

public sealed class GetAdminReportDetailsQueryHandler
    : IRequestHandler<GetAdminReportDetailsQuery, AdminReportResponse>
{
    private readonly IReportRepository _reportRepository;

    public GetAdminReportDetailsQueryHandler(IReportRepository reportRepository)
    {
        _reportRepository = reportRepository;
    }

    public async Task<AdminReportResponse> Handle(
        GetAdminReportDetailsQuery request,
        CancellationToken cancellationToken)
    {
        return await _reportRepository.GetAdminReportByIdAsync(
            request.ReportId,
            cancellationToken)
            ?? throw new ReportException(
                "report.not_found",
                "Không tìm thấy báo cáo.",
                404);
    }
}
