using MediatR;

namespace SafeRide.Application.Features.AdminReports.Queries.GetAdminReports;

public sealed record GetAdminReportsQuery(
    int Page = 1,
    int PageSize = 10,
    string? Status = null,
    string? Search = null) : IRequest<AdminReportPagedResult>;
