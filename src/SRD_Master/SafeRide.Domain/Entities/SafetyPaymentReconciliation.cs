using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class SafetyPaymentReconciliation
{
    public long Id { get; set; }
    public long TripId { get; set; }
    public decimal CustomerPayableAmount { get; set; }
    public decimal SuccessfulPaymentAmount { get; set; }
    public decimal RemainingPayableAmount { get; set; }
    public decimal RefundObligationAmount { get; set; }
    public decimal DriverCreditedAmount { get; set; }
    public SafetyPaymentReconciliationStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Trip Trip { get; set; } = null!;
    public ManualPaymentRefund? Refund { get; set; }
}
