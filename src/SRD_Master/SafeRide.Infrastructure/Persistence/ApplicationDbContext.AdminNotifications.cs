using Microsoft.EntityFrameworkCore;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Persistence.Configurations;

namespace SafeRide.Infrastructure.Persistence;

public partial class ApplicationDbContext
{
    public virtual DbSet<AccountBanConfiguration> AccountBanConfigurations { get; set; }

    public virtual DbSet<AccountBanHistory> AccountBanHistories { get; set; }

    public virtual DbSet<AdminNotification> AdminNotifications { get; set; }

    public virtual DbSet<RiskProtectionPolicyVersion> RiskProtectionPolicyVersions { get; set; }
    public virtual DbSet<TripFinancialSettlement> TripFinancialSettlements { get; set; }
    public virtual DbSet<PreTripVehicleCheck> PreTripVehicleChecks { get; set; }
    public virtual DbSet<TripProtectionCoverage> TripProtectionCoverages { get; set; }
    public virtual DbSet<VehicleInsurancePolicy> VehicleInsurancePolicies { get; set; }
    public virtual DbSet<InsurancePolicyDocument> InsurancePolicyDocuments { get; set; }
    public virtual DbSet<AccidentReport> AccidentReports { get; set; }
    public virtual DbSet<AccidentEvidence> AccidentEvidence { get; set; }
    public virtual DbSet<AccidentLiabilityAssessment> AccidentLiabilityAssessments { get; set; }
    public virtual DbSet<AccidentLiabilityCause> AccidentLiabilityCauses { get; set; }
    public virtual DbSet<LiabilityDisputeAudit> LiabilityDisputeAudits { get; set; }
    public virtual DbSet<LiabilityDisputeEvidence> LiabilityDisputeEvidence { get; set; }
    public virtual DbSet<ProtectionClaim> ProtectionClaims { get; set; }
    public virtual DbSet<InsuranceClaimDocument> InsuranceClaimDocuments { get; set; }
    public virtual DbSet<DriverLiability> DriverLiabilities { get; set; }
    public virtual DbSet<ClaimRecovery> ClaimRecoveries { get; set; }
    public virtual DbSet<ClaimReconciliationRecord> ClaimReconciliationRecords { get; set; }
    public virtual DbSet<InsuranceClaimProviderAudit> InsuranceClaimProviderAudits { get; set; }
    public virtual DbSet<RiskFundAccount> RiskFundAccounts { get; set; }
    public virtual DbSet<RiskFundTransaction> RiskFundTransactions { get; set; }
    public virtual DbSet<SafetyTerminationEvidence> SafetyTerminationEvidence { get; set; }
    public virtual DbSet<SafetyPaymentReconciliation> SafetyPaymentReconciliations { get; set; }
    public virtual DbSet<ManualPaymentRefund> ManualPaymentRefunds { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AccountBanConfigurationConfiguration());
        modelBuilder.ApplyConfiguration(new AccountBanHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new AdminNotificationConfiguration());
        RiskProtectionModelConfiguration.Configure(modelBuilder);
    }
}
