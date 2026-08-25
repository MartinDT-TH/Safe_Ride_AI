using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class ProtectionClaim
{
    public long Id { get; set; }
    public long AccidentReportId { get; set; }
    public ProtectionClaimStatus Status { get; set; } = ProtectionClaimStatus.DRAFT;
    public InsuranceClaimStatus InsuranceStatus { get; set; } = InsuranceClaimStatus.NOT_SUBMITTED;
    public InsurancePaymentDestination InsurancePaymentDestination { get; set; } = InsurancePaymentDestination.DIRECT_TO_CLAIMANT;
    public string? InsuranceReference { get; set; }
    public decimal InsuranceRequestedAmount { get; set; }
    public decimal TotalDamageAmount { get; set; }
    public decimal EligibleDamageAmount { get; set; }
    public decimal InsuranceApprovedAmount { get; set; }
    public decimal InsurancePaidDirectToClaimant { get; set; }
    public decimal InsuranceReimbursedToRiskFund { get; set; }
    public decimal RiskFundAdvanceAmount { get; set; }
    public decimal RiskFundPermanentLossAmount { get; set; }
    public decimal DriverLiabilityAmount { get; set; }
    public decimal CustomerLiabilityAmount { get; set; }
    public decimal ThirdPartyLiabilityAmount { get; set; }
    public decimal TotalPaidToClaimant { get; set; }
    public decimal RecoveredAmount { get; set; }
    public decimal OutstandingRecoveryAmount { get; set; }
    public decimal WrittenOffAdvanceAmount { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public AccidentReport AccidentReport { get; set; } = null!;
    public ICollection<DriverLiability> DriverLiabilities { get; set; } = new List<DriverLiability>();
    public ICollection<ClaimRecovery> Recoveries { get; set; } = new List<ClaimRecovery>();
    public ICollection<ClaimReconciliationRecord> ReconciliationRecords { get; set; } = new List<ClaimReconciliationRecord>();
    public ICollection<InsuranceClaimProviderAudit> InsuranceProviderAudits { get; set; } = new List<InsuranceClaimProviderAudit>();
}
