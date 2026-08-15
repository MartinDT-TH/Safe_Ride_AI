namespace SafeRide.Application.Features.StaffNotifications;

public sealed record StaffNotificationRequestPagedResult(
    IReadOnlyCollection<StaffNotificationRequestResponse> Items,
    StaffNotificationRequestCountsResponse Counts,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
