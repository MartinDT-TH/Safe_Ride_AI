namespace SafeRide.Application.Features.AdminNotifications;

public sealed record AdminNotificationCountsResponse(
    int All,
    int Pending,
    int Approved,
    int Rejected);
