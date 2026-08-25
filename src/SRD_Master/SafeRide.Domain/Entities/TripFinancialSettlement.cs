namespace SafeRide.Domain.Entities;

public sealed class TripFinancialSettlement
{
    public const int CurrentComponentBreakdownVersion = 1;

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
    public int? ComponentBreakdownVersion { get; set; }
    public decimal? GrossFare { get; set; }
    public decimal? FareComponent { get; set; }
    public decimal? LongDistanceComponent { get; set; }
    public decimal? SnapshotPromotionDiscount { get; set; }
    public decimal? AppliedPromotionDiscount { get; set; }
    public decimal? DriverFareEarning { get; set; }
    public decimal? LongDistanceEarning { get; set; }
    public decimal? LongPickupCompensation { get; set; }
    public decimal? DriverPayout { get; set; }
    public bool IsRiskContributionEligible { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SettledAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Trip Trip { get; set; } = null!;
    public RiskProtectionPolicyVersion PolicyVersion { get; set; } = null!;
}
