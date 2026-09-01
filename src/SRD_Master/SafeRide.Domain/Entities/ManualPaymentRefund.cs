using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class ManualPaymentRefund
{
    public long Id { get; set; }
    public long SafetyPaymentReconciliationId { get; set; }
    public long PaymentId { get; set; }
    public decimal Amount { get; set; }
    public ManualRefundStatus Status { get; set; } = ManualRefundStatus.REFUND_PENDING;
    public string? PaymentReference { get; set; }
    public string? EvidenceUrl { get; set; }
    public Guid? RefundedByUserId { get; set; }
    public string? ConfirmationIdempotencyKey { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RefundedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public SafetyPaymentReconciliation Reconciliation { get; set; } = null!;
    public Payment Payment { get; set; } = null!;
}
