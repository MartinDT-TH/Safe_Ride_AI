namespace SafeRide.Application.Features.AdminBookings;

public sealed record AdminBookingPagedResult(
    IReadOnlyList<AdminBookingResponse> Items,
    AdminBookingCountsResponse Counts,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
