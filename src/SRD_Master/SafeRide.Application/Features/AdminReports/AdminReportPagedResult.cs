namespace SafeRide.Application.Features.AdminReports;

public sealed record AdminReportPagedResult(
    IReadOnlyList<AdminReportResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
