using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class RiskFundTransaction
{
    public long Id { get; set; }
    public long RiskFundAccountId { get; set; }
    public RiskFundTransactionType TransactionType { get; set; }
    public LedgerDirection Direction { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public long? TripId { get; set; }
    public long? ProtectionClaimId { get; set; }
    public long? ClaimRecoveryId { get; set; }
    public Guid? PerformedByUserId { get; set; }
    public string? ExternalReference { get; set; }
    public string? EvidenceUrl { get; set; }
    public string Reason { get; set; } = null!;
    public string IdempotencyKey { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public RiskFundAccount RiskFundAccount { get; set; } = null!;
}
