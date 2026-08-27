namespace SafeRide.Domain.Entities;

public sealed class RiskProtectionPolicyVersion
{
    public long Id { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public decimal BasePlatformCommissionRate { get; set; }
    public decimal RiskReserveRate { get; set; }
    public decimal DefaultProtectionLimit { get; set; }
    public decimal DriverOrdinaryNegligenceRate { get; set; }
    public decimal DriverOrdinaryNegligenceCap { get; set; }
    public decimal DriverGrossNegligenceRate { get; set; }
    public decimal DriverGrossNegligenceCap { get; set; }
    public decimal MockInsuranceCoverageLimit { get; set; }
    public decimal ClaimAutoApprovalThreshold { get; set; }
    public bool RiskFundEnabled { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string ChangeReason { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
