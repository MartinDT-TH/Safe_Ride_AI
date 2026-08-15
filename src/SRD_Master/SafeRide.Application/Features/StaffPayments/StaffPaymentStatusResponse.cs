using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.StaffPayments;

public sealed record StaffPaymentStatusResponse(
    long Id,
    long TripId,
    long BookingId,
    string CustomerName,
    string MaskedPhone,
    decimal Amount,
    string Currency,
    PaymentMethod Method,
    PaymentStatus Status,
    DateTime PerformedAt);
