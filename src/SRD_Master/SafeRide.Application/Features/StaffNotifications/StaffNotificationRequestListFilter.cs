namespace SafeRide.Application.Features.StaffNotifications;

public sealed record StaffNotificationRequestListFilter(
    Guid CreatedBy,
    int Page,
    int PageSize,
    string? Search,
    string? Status,
    string? Type,
    string? Audience);
