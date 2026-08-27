using Microsoft.EntityFrameworkCore;
using SafeRide.Domain.Entities;

namespace SafeRide.Infrastructure.Persistence;

public partial class ApplicationDbContext
{
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureRiskFundLedgerIsAppendOnly();
        EnsureReferencedRiskProtectionPoliciesAreImmutable();
        EnsurePreTripVehicleChecksAreImmutable();
        EnsureInsuranceProviderAuditsAreImmutable();
        EnsureClaimRecoveriesAreImmutable();
        EnsureClaimReconciliationRecordsAreImmutable();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureRiskFundLedgerIsAppendOnly();
        await EnsureReferencedRiskProtectionPoliciesAreImmutableAsync(cancellationToken);
        EnsurePreTripVehicleChecksAreImmutable();
        EnsureInsuranceProviderAuditsAreImmutable();
        EnsureClaimRecoveriesAreImmutable();
        EnsureClaimReconciliationRecordsAreImmutable();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureRiskFundLedgerIsAppendOnly()
    {
        var invalidEntry = ChangeTracker.Entries<RiskFundTransaction>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (invalidEntry is null) return;

        throw new InvalidOperationException(
            "Risk Fund ledger transactions are immutable. Record corrections with an audited ADJUSTMENT transaction.");
    }

    private void EnsureReferencedRiskProtectionPoliciesAreImmutable()
    {
        var policyIds = ChangedPolicyIds();
        if (policyIds.Count == 0) return;
        if (TripProtectionCoverages.Any(x => policyIds.Contains(x.PolicyVersionId))
            || TripFinancialSettlements.Any(x => policyIds.Contains(x.PolicyVersionId)))
            throw ReferencedPolicyIsImmutable();
    }

    private async Task EnsureReferencedRiskProtectionPoliciesAreImmutableAsync(
        CancellationToken cancellationToken)
    {
        var policyIds = ChangedPolicyIds();
        if (policyIds.Count == 0) return;
        if (await TripProtectionCoverages.AnyAsync(
                x => policyIds.Contains(x.PolicyVersionId), cancellationToken)
            || await TripFinancialSettlements.AnyAsync(
                x => policyIds.Contains(x.PolicyVersionId), cancellationToken))
            throw ReferencedPolicyIsImmutable();
    }

    private HashSet<long> ChangedPolicyIds() => ChangeTracker
        .Entries<RiskProtectionPolicyVersion>()
        .Where(entry => entry.State is EntityState.Modified or EntityState.Deleted)
        .Select(entry => entry.Entity.Id)
        .Where(id => id > 0)
        .ToHashSet();

    private static InvalidOperationException ReferencedPolicyIsImmutable() => new(
        "Risk Protection policy versions referenced by coverage or settlement snapshots are immutable. Create a new version instead.");

    private void EnsurePreTripVehicleChecksAreImmutable()
    {
        var invalidEntry = ChangeTracker.Entries<PreTripVehicleCheck>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (invalidEntry is null) return;

        throw new InvalidOperationException(
            "Pre-trip vehicle safety check attempts are immutable. Record a new attempt instead.");
    }

    private void EnsureInsuranceProviderAuditsAreImmutable()
    {
        var invalidEntry = ChangeTracker.Entries<InsuranceClaimProviderAudit>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (invalidEntry is null) return;

        throw new InvalidOperationException(
            "Insurance provider audit records are immutable. Record a new provider operation instead.");
    }

    private void EnsureClaimRecoveriesAreImmutable()
    {
        var invalidEntry = ChangeTracker.Entries<ClaimRecovery>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (invalidEntry is null) return;

        throw new InvalidOperationException(
            "Claim recovery records are immutable. Record corrections with an audited Risk Fund ADJUSTMENT transaction.");
    }

    private void EnsureClaimReconciliationRecordsAreImmutable()
    {
        var invalidEntry = ChangeTracker.Entries<ClaimReconciliationRecord>()
            .FirstOrDefault(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (invalidEntry is null) return;

        throw new InvalidOperationException(
            "Claim reconciliation records are immutable. Append a new audited record instead.");
    }
}
