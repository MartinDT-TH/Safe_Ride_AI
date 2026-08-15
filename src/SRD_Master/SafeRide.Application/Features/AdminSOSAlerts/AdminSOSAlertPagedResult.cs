namespace SafeRide.Application.Features.AdminSOSAlerts;

public sealed record AdminSOSAlertPagedResult(
    IReadOnlyList<AdminSOSAlertResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
