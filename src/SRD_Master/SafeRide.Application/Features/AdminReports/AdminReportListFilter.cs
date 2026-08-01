using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.AdminReports;

public sealed record AdminReportListFilter(
    int Page,
    int PageSize,
    ReportStatus? Status,
    string? Search);
