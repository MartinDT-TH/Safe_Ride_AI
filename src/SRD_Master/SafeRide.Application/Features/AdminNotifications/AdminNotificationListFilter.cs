namespace SafeRide.Application.Features.AdminNotifications;

public sealed record AdminNotificationListFilter(
    int Page = 1,
    int PageSize = 10,
    string? Search = null,
    string? Status = null,
    string? Type = null,
    string? Audience = null);
