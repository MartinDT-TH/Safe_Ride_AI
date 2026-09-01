using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class DriverLiability
{
    public long Id { get; set; }
    public long ProtectionClaimId { get; set; }
    public Guid DriverId { get; set; }
    public decimal DriverAttributableEligibleDamage { get; set; }
    public DriverFaultLevel FaultLevel { get; set; }
    public decimal AppliedRate { get; set; }
    public decimal? AppliedCap { get; set; }
    public decimal ConfirmedAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public DriverLiabilityStatus Status { get; set; } = DriverLiabilityStatus.CONFIRMED;
    public string? DisputeReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ProtectionClaim ProtectionClaim { get; set; } = null!;
}
