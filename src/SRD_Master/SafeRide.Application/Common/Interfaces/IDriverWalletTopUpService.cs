using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Interfaces;

public interface IDriverWalletTopUpService
{
    Task<WalletTopUpResult> CreateAsync(Guid driverId, decimal amount, string? returnUrl, string? cancelUrl, CancellationToken cancellationToken);
    Task<WalletTopUpResult> GetStatusAsync(Guid driverId, long topUpId, CancellationToken cancellationToken);
    Task<bool> TryHandleWebhookAsync(PayOsWebhookRequest request, CancellationToken cancellationToken);
}

public sealed record WalletTopUpResult(
    long TopUpId,
    long OrderCode,
    decimal Amount,
    PaymentStatus Status,
    string? QrCode,
    string? CheckoutUrl,
    decimal? CurrentBalance,
    DateTime CreatedAt,
    DateTime? PaidAt,
    string Message);
