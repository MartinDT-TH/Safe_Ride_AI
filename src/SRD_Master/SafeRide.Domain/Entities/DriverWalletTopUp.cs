using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class DriverWalletTopUp
{
    public long Id { get; set; }
    public long WalletId { get; set; }
    public long OrderCode { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? PaymentLinkId { get; set; }
    public string? ProviderReference { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public DriverWallet Wallet { get; set; } = null!;
}
