using MediatR;

namespace SafeRide.Application.Features.AdminReports.Queries.GetAdminReportDetails;

public sealed record GetAdminReportDetailsQuery(long ReportId)
    : IRequest<AdminReportResponse>;
