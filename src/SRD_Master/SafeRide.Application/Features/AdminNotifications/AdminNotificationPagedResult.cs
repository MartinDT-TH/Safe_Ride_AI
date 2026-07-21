namespace SafeRide.Application.Features.AdminNotifications;

public sealed record AdminNotificationPagedResult(
    IReadOnlyList<AdminNotificationResponse> Items,
    AdminNotificationCountsResponse Counts,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
