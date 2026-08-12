namespace SafeRide.Application.Features.StaffNotifications;

public sealed record StaffNotificationRequestCountsResponse(
    int All,
    int Pending,
    int Approved,
    int Rejected);
