namespace SafeRide.Application.Features.AdminBookings;

public sealed record AdminBookingCountsResponse(
    int Total,
    int Pending,
    int Scheduled,
    int CancelledOrExpired,
    DateTime? NextScheduledAt,
    int? NextScheduledInMinutes);
