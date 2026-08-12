namespace SafeRide.Application.Features.StaffPayments;

public sealed record StaffPaymentStatusCountsResponse(
    int Total,
    int Pending,
    int Success,
    int Failed,
    int Cancelled);
