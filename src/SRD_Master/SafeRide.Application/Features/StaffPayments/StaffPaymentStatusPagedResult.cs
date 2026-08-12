namespace SafeRide.Application.Features.StaffPayments;

public sealed record StaffPaymentStatusPagedResult(
    StaffPaymentStatusCountsResponse Counts,
    IReadOnlyCollection<StaffPaymentStatusResponse> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
