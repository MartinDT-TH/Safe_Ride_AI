using MediatR;

namespace SafeRide.Application.Features.AdminReports.Commands.UpdateAdminReportStatus;

public sealed record UpdateAdminReportStatusCommand(long ReportId, string Status)
    : IRequest<AdminReportResponse>;
