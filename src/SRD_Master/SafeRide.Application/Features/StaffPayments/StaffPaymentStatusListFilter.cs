namespace SafeRide.Application.Features.StaffPayments;

public sealed record StaffPaymentStatusListFilter(
    int Page,
    int PageSize,
    string? Status,
    string? Method,
    DateOnly? Date);
