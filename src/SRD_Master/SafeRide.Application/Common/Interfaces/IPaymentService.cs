using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Interfaces;

public interface IPaymentService
{
    Task<QrPaymentResult> CreateQrPaymentAsync(
        Guid customerId,
        long tripId,
        string? returnUrl,
        string? cancelUrl,
        CancellationToken cancellationToken);

    Task<QrPaymentResult> CreateDriverQrPaymentAsync(
        Guid driverId,
        long tripId,
        string? returnUrl,
        string? cancelUrl,
        CancellationToken cancellationToken);

    Task<PaymentStatusResult> GetTripPaymentStatusAsync(
        Guid customerId,
        long tripId,
        CancellationToken cancellationToken);

    Task<PaymentStatusResult> GetDriverTripPaymentStatusAsync(
        Guid driverId,
        long tripId,
        CancellationToken cancellationToken);

    Task<PaymentStatusResult> ConfirmCashPaymentAsync(
        Guid driverId,
        long tripId,
        CancellationToken cancellationToken);

    Task HandlePayOsWebhookAsync(
        PayOsWebhookRequest request,
        CancellationToken cancellationToken);
}

public sealed record QrPaymentResult(
    long TripId,
    long PaymentId,
    string OrderCode,
    decimal Amount,
    string Currency,
    PaymentStatus PaymentStatus,
    string? QrCode,
    string? CheckoutUrl,
    DateTime CreatedAt);

public sealed record PaymentStatusResult(
    long TripId,
    long? PaymentId,
    PaymentMethod? PaymentMethod,
    PaymentStatus PaymentStatus,
    decimal Amount,
    decimal OriginalFare,
    decimal FinalFare,
    decimal DriverShare,
    decimal PlatformShare,
    string Currency,
    DateTime? PaidAt);

public sealed record PayOsWebhookRequest(
    string Code,
    string Desc,
    bool Success,
    PayOsWebhookData? Data,
    string Signature);

public sealed record PayOsWebhookData(
    long OrderCode,
    decimal Amount,
    string? Description,
    string? AccountNumber,
    string? Reference,
    string? TransactionDateTime,
    string? Currency,
    string? PaymentLinkId,
    string? Code,
    string? Desc,
    string? CounterAccountBankId,
    string? CounterAccountBankName,
    string? CounterAccountName,
    string? CounterAccountNumber,
    string? VirtualAccountName,
    string? VirtualAccountNumber);
