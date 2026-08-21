namespace SafeRide.Domain.Entities;

public sealed class TripFinancialSettlement
{
    public long Id { get; set; }
    public long TripId { get; set; }
    public long PolicyVersionId { get; set; }
    public decimal CommissionBase { get; set; }
    public decimal PromotionExpense { get; set; }
    public decimal CustomerPayableAmount { get; set; }
    public decimal PlatformCommissionRate { get; set; }
    public decimal GrossPlatformCommission { get; set; }
    public decimal DriverEarning { get; set; }
    public decimal NetPlatformCommission { get; set; }
    public decimal RiskReserveRate { get; set; }
    public decimal RiskContribution { get; set; }
    public decimal NetOperatingRevenue { get; set; }
    public bool IsRiskContributionEligible { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SettledAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Trip Trip { get; set; } = null!;
    public RiskProtectionPolicyVersion PolicyVersion { get; set; } = null!;
}
