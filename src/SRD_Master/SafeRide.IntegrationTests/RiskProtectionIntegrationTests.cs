using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.API.Controllers;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Services;

namespace SafeRide.IntegrationTests;

[Trait(SqlServerTestDatabase.ProviderTraitName, SqlServerTestDatabase.InMemoryProvider)]
public sealed class RiskProtectionIntegrationTests
{
    [Fact]
    public async Task FileSafetyScanner_GuardsProductionAndDoesNotLabelDevelopmentBypassClean()
    {
        await using var content = new MemoryStream([0xFF, 0xD8, 0xFF]);
        var production = await new UnconfiguredFileSafetyScanner().ScanAsync(
            "evidence.jpg", "image/jpeg", content, CancellationToken.None);
        content.Position = 0;
        var development = await new NonProductionFileSafetyScanner().ScanAsync(
            "evidence.jpg", "image/jpeg", content, CancellationToken.None);

        Assert.Equal(FileSafetyScanStatus.ScannerUnavailable, production.Status);
        Assert.Equal(FileSafetyScanStatus.DevelopmentBypass, development.Status);
        Assert.NotEqual(FileSafetyScanStatus.Clean, development.Status);
    }

    [Fact]
    public async Task RiskFund_DebitLargerThanBalance_DoesNotCreatePartialOrNegativeTransaction()
    {
        await using var db = CreateDbContext();
        var ledger = new RiskFundLedgerService(db);
        var actor = Guid.NewGuid();

        await ledger.ApplyOpeningBalanceAsync(actor, new RiskFundMutationRequest(
            100_000m, LedgerDirection.CREDIT, "MVP opening balance", "BANK-001",
            "https://evidence.test/opening.pdf", "opening-001"), CancellationToken.None);

        var applied = await ledger.ApplyAsync(
            RiskFundTransactionType.CLAIM_ADVANCE, LedgerDirection.DEBIT, 120_000m,
            null, 10, null, actor, "CLM-10", null, "Claim funding", "claim-10",
            CancellationToken.None);

        Assert.False(applied);
        Assert.Equal(100_000m, (await db.RiskFundAccounts.SingleAsync()).CurrentBalance);
        Assert.Single(await db.RiskFundTransactions.ToListAsync());
    }

    [Fact]
    public async Task RiskFund_OpeningBalance_CannotBeBackfilledTwice()
    {
        await using var db = CreateDbContext();
        var ledger = new RiskFundLedgerService(db);
        var actor = Guid.NewGuid();
        await ledger.ApplyOpeningBalanceAsync(actor, Mutation("opening-001"), CancellationToken.None);

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            ledger.ApplyOpeningBalanceAsync(actor, Mutation("opening-002"), CancellationToken.None));

        Assert.Equal("risk_fund.opening_balance_exists", exception.Code);
        Assert.Single(await db.RiskFundTransactions.ToListAsync());
    }

    [Fact]
    public async Task RiskFund_IdempotentRetry_ReturnsOriginalTransaction()
    {
        await using var db = CreateDbContext();
        var ledger = new RiskFundLedgerService(db);
        var actor = Guid.NewGuid();
        var request = Mutation(" opening-001 ");

        var first = await ledger.ApplyOpeningBalanceAsync(actor, request, CancellationToken.None);
        var replay = await ledger.ApplyOpeningBalanceAsync(actor, request, CancellationToken.None);

        Assert.True(first.Applied);
        Assert.False(replay.Applied);
        Assert.Equal(first.Transaction.Id, replay.Transaction.Id);
        Assert.Equal("opening-001", replay.Transaction.IdempotencyKey);
        Assert.Single(await db.RiskFundTransactions.ToListAsync());
    }

    [Fact]
    public async Task RiskFund_ReusedIdempotencyKeyWithDifferentPayload_IsRejected()
    {
        await using var db = CreateDbContext();
        var ledger = new RiskFundLedgerService(db);
        var actor = Guid.NewGuid();
        await ledger.ApplyOpeningBalanceAsync(actor, Mutation("opening-001"), CancellationToken.None);

        var conflictingRequest = Mutation("opening-001") with { Amount = 200_000m };
        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            ledger.ApplyOpeningBalanceAsync(actor, conflictingRequest, CancellationToken.None));

        Assert.Equal("risk_fund.idempotency_conflict", exception.Code);
        Assert.Single(await db.RiskFundTransactions.ToListAsync());
        Assert.Equal(100_000m, (await db.RiskFundAccounts.SingleAsync()).CurrentBalance);
    }

    [Fact]
    public async Task RiskFund_DebitAdjustmentWithInsufficientBalance_ReturnsConflictWithoutPartialDebit()
    {
        await using var db = CreateDbContext();
        var ledger = new RiskFundLedgerService(db);
        var actor = Guid.NewGuid();
        await ledger.ApplyOpeningBalanceAsync(actor, Mutation("opening-001"), CancellationToken.None);

        var request = new RiskFundMutationRequest(
            120_000m,
            LedgerDirection.DEBIT,
            "Audited correction",
            "BANK-ADJ-001",
            "https://evidence.test/adjustment.pdf",
            "adjustment-001");
        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            ledger.ApplyAdjustmentAsync(actor, request, CancellationToken.None));

        Assert.Equal("risk_fund.insufficient_balance", exception.Code);
        Assert.Equal(100_000m, (await db.RiskFundAccounts.SingleAsync()).CurrentBalance);
        Assert.Single(await db.RiskFundTransactions.ToListAsync());
    }

    [Fact]
    public async Task RiskFund_LedgerTransaction_CannotBeUpdatedOrDeleted()
    {
        await using var db = CreateDbContext();
        var ledger = new RiskFundLedgerService(db);
        await ledger.ApplyOpeningBalanceAsync(Guid.NewGuid(), Mutation("opening-001"), CancellationToken.None);
        var transaction = await db.RiskFundTransactions.SingleAsync();

        transaction.Reason = "Attempted rewrite";
        var updateException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            db.SaveChangesAsync());
        Assert.Contains("immutable", updateException.Message, StringComparison.OrdinalIgnoreCase);

        db.Entry(transaction).State = EntityState.Unchanged;
        db.RiskFundTransactions.Remove(transaction);
        var deleteException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            db.SaveChangesAsync());
        Assert.Contains("immutable", deleteException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RiskFund_Model_HasConcurrencyAndAccountingGuards()
    {
        using var db = CreateDbContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var accountType = model.FindEntityType(typeof(RiskFundAccount));
        var rowVersion = accountType!.FindProperty(nameof(RiskFundAccount.RowVersion));
        Assert.True(rowVersion!.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
        Assert.Contains(
            "CK_RiskFundAccounts_Singleton",
            accountType.GetCheckConstraints().Select(constraint => constraint.Name));

        var transactionType = model.FindEntityType(typeof(RiskFundTransaction));
        var checkConstraints = transactionType!.GetCheckConstraints()
            .Select(constraint => constraint.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("CK_RiskFundTransactions_BalanceMovement", checkConstraints);
        Assert.Contains("CK_RiskFundTransactions_TypeDirection", checkConstraints);
        Assert.Contains("CK_RiskFundTransactions_TypeLinks", checkConstraints);
        Assert.Contains("CK_RiskFundTransactions_AdministrativeAudit", checkConstraints);

        var recoveryConstraints = model.FindEntityType(typeof(ClaimRecovery))!
            .GetCheckConstraints()
            .Select(constraint => constraint.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("CK_ClaimRecoveries_Amount", recoveryConstraints);
        Assert.Contains("CK_ClaimRecoveries_Audit", recoveryConstraints);
    }

    [Fact]
    public async Task PreTrip_LatestPass_ActivatesCoverageWithoutVehicleInsurance()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(db, TripStatus.ARRIVED, riskEnabled: true);
        var policyProvider = new RiskProtectionPolicyProvider(db);
        var service = new PreTripVehicleCheckService(
            db,
            policyProvider,
            new SystemDateTimeProvider());

        await service.CreateAsync(graph.DriverId, graph.Trip.Id, FailedCheck(), null, CancellationToken.None);
        var pass = await service.CreateAsync(graph.DriverId, graph.Trip.Id, PassedCheck(), null, CancellationToken.None);
        await service.EnsureCanStartAndActivateCoverageAsync(
            graph.DriverId, graph.Trip, DateTime.UtcNow, CancellationToken.None);
        await db.SaveChangesAsync();
        await service.EnsureCanStartAndActivateCoverageAsync(
            graph.DriverId, graph.Trip, DateTime.UtcNow, CancellationToken.None);
        await db.SaveChangesAsync();

        var coverage = await db.TripProtectionCoverages.SingleAsync();
        Assert.Equal(PreTripCheckResult.PASS, pass.Result);
        Assert.Equal(graph.DriverId, pass.DriverId);
        Assert.True(pass.BrakeResponsePassed);
        Assert.True(pass.FrontRearLightsPassed);
        Assert.True(pass.TurnSignalsPassed);
        Assert.True(pass.VisibleTiresPassed);
        Assert.True(pass.DashboardWarningPassed);
        Assert.True(pass.WindshieldVisibilityPassed);
        Assert.True(pass.NoMajorVisibleIssue);
        Assert.Equal(pass.Id, coverage.PreTripVehicleCheckId);
        Assert.Null(coverage.VehicleInsurancePolicyId);
    }

    [Fact]
    public async Task PreTrip_Attempt_CannotBeUpdatedOrDeleted()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(db, TripStatus.ARRIVED, riskEnabled: true);
        var service = new PreTripVehicleCheckService(
            db,
            new RiskProtectionPolicyProvider(db),
            new SystemDateTimeProvider());
        var created = await service.CreateAsync(
            graph.DriverId,
            graph.Trip.Id,
            PassedCheck(),
            null,
            CancellationToken.None);
        var attempt = await db.PreTripVehicleChecks.SingleAsync(x => x.Id == created.Id);

        attempt.Note = "Attempted rewrite";
        var updateException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            db.SaveChangesAsync());
        Assert.Contains("immutable", updateException.Message, StringComparison.OrdinalIgnoreCase);

        db.Entry(attempt).State = EntityState.Unchanged;
        db.PreTripVehicleChecks.Remove(attempt);
        var deleteException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            db.SaveChangesAsync());
        Assert.Contains("immutable", deleteException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PreTrip_TrustedUploadedEvidence_PersistsAuditableMetadata()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(db, TripStatus.ARRIVED, riskEnabled: true);
        var service = new PreTripVehicleCheckService(
            db,
            new RiskProtectionPolicyProvider(db),
            new SystemDateTimeProvider());
        var evidence = new StoredPreTripVehicleCheckEvidence(
            "https://storage.test/pre-trip/brake.jpg",
            "saferide/pre-trip/check-1",
            "brake.jpg",
            "image/jpeg",
            512);

        var result = await service.CreateAsync(
            graph.DriverId,
            graph.Trip.Id,
            FailedCheck(),
            evidence,
            CancellationToken.None);

        Assert.Equal(evidence.FileUrl, result.EvidenceUrl);
        Assert.Equal(evidence.OriginalFileName, result.EvidenceOriginalFileName);
        Assert.Equal(evidence.ContentType, result.EvidenceContentType);
        Assert.Equal(evidence.FileSizeBytes, result.EvidenceFileSizeBytes);
        var persisted = await db.PreTripVehicleChecks.SingleAsync(x => x.Id == result.Id);
        Assert.Equal(evidence.StoragePublicId, persisted.EvidenceStoragePublicId);
    }

    [Fact]
    public async Task PreTrip_ClientSuppliedEvidenceUrl_IsRejected()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(db, TripStatus.ARRIVED, riskEnabled: true);
        var service = new PreTripVehicleCheckService(
            db,
            new RiskProtectionPolicyProvider(db),
            new SystemDateTimeProvider());
        var request = FailedCheck() with
        {
            EvidenceUrl = "https://untrusted.test/evidence.jpg"
        };

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            service.CreateAsync(
                graph.DriverId,
                graph.Trip.Id,
                request,
                null,
                CancellationToken.None));

        Assert.Equal("pretrip.external_evidence_not_allowed", exception.Code);
        Assert.Empty(await db.PreTripVehicleChecks.ToListAsync());
    }

    [Fact]
    public async Task PreTrip_Access_IsLimitedToAssignedDriverParticipantsAndManagement()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(db, TripStatus.ARRIVED, riskEnabled: true);
        var service = new PreTripVehicleCheckService(
            db,
            new RiskProtectionPolicyProvider(db),
            new SystemDateTimeProvider());
        await service.CreateAsync(
            graph.DriverId,
            graph.Trip.Id,
            PassedCheck(),
            null,
            CancellationToken.None);

        Assert.Single(await service.GetAsync(
            graph.DriverId, false, graph.Trip.Id, CancellationToken.None));
        Assert.Single(await service.GetAsync(
            graph.Trip.Booking.CustomerId, false, graph.Trip.Id, CancellationToken.None));
        Assert.Single(await service.GetAsync(
            Guid.NewGuid(), true, graph.Trip.Id, CancellationToken.None));

        var outsider = await Assert.ThrowsAsync<BookingException>(() =>
            service.GetAsync(
                Guid.NewGuid(), false, graph.Trip.Id, CancellationToken.None));
        Assert.Equal("trip.not_found", outsider.Code);

        var unassignedDriver = await Assert.ThrowsAsync<BookingException>(() =>
            service.CreateAsync(
                Guid.NewGuid(),
                graph.Trip.Id,
                PassedCheck(),
                null,
                CancellationToken.None));
        Assert.Equal("trip.not_found", unassignedDriver.Code);
    }

    [Fact]
    public async Task VehicleInsurance_CrudEnforcesOwnership_AndStaffReviewCapturesAuditActorAndTime()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(db, TripStatus.ARRIVED, riskEnabled: true);
        var ownerId = graph.Trip.Booking.CustomerId;
        var vehicleId = graph.Trip.Booking.VehicleId;
        var outsiderId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var service = new VehicleInsurancePolicyService(db, new SystemDateTimeProvider());
        var request = new VehicleInsurancePolicyRequest(
            VehicleInsuranceType.PHYSICAL_DAMAGE,
            "Safe Insurer",
            $"POL-{Guid.NewGuid():N}",
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddYears(1),
            20_000_000m,
            500_000m,
            "https://storage.test/policy.pdf");

        var ownershipError = await Assert.ThrowsAsync<BookingException>(() =>
            service.CreateAsync(outsiderId, vehicleId, request, CancellationToken.None));
        Assert.Equal("vehicle.not_found", ownershipError.Code);

        var created = await service.CreateAsync(ownerId, vehicleId, request, CancellationToken.None);
        Assert.Equal(InsuranceVerificationStatus.PENDING, created.VerificationStatus);

        var reviewed = await service.ReviewAsync(
            staffId,
            created.Id,
            InsuranceVerificationStatus.VERIFIED,
            CancellationToken.None);
        Assert.Equal(InsuranceVerificationStatus.VERIFIED, reviewed.VerificationStatus);
        Assert.Equal(staffId, reviewed.ReviewedByUserId);
        Assert.NotNull(reviewed.ReviewedAtUtc);

        var updateError = await Assert.ThrowsAsync<BookingException>(() =>
            service.UpdateAsync(outsiderId, vehicleId, created.Id, request, CancellationToken.None));
        Assert.Equal("vehicle.not_found", updateError.Code);

        var updated = await service.UpdateAsync(
            ownerId,
            vehicleId,
            created.Id,
            request with { CoverageAmount = 25_000_000m },
            CancellationToken.None);
        Assert.Equal(InsuranceVerificationStatus.PENDING, updated.VerificationStatus);
        Assert.Null(updated.ReviewedByUserId);
        Assert.Null(updated.ReviewedAtUtc);
    }

    [Fact]
    public void PreTrip_Model_OrdersLatestAttemptByTripAndDescendingCheckTime()
    {
        using var db = CreateDbContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var entityType = model.FindEntityType(typeof(PreTripVehicleCheck));
        var index = entityType!.GetIndexes().Single(candidate =>
            candidate.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(PreTripVehicleCheck.TripId), nameof(PreTripVehicleCheck.CheckedAtUtc)]));

        Assert.Equal([false, true], index.IsDescending);
        Assert.Contains(
            "CK_PreTripVehicleChecks_EvidenceFileSize",
            entityType.GetCheckConstraints().Select(constraint => constraint.Name));
        Assert.NotNull(model.FindEntityType(typeof(Vehicle))!
            .FindNavigation(nameof(Vehicle.VehicleInsurancePolicies)));
    }

    [Fact]
    public async Task CompletedEligibleTrip_CreatesOneContributionFromNetCommissionAfterPromotion()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(db, TripStatus.COMPLETED, riskEnabled: true);
        graph.Trip.ActualFare = 100_000m;
        graph.Trip.Booking.BookingPromotions.Add(new BookingPromotion
        {
            BookingId = graph.Trip.BookingId,
            PromotionId = graph.Promotion.Id,
            DiscountAmount = 20_000m,
            CreatedAt = DateTime.UtcNow
        });
        var check = new PreTripVehicleCheck
        {
            TripId = graph.Trip.Id,
            DriverId = graph.DriverId,
            BrakeResponsePassed = true,
            FrontRearLightsPassed = true,
            TurnSignalsPassed = true,
            VisibleTiresPassed = true,
            DashboardWarningPassed = true,
            WindshieldVisibilityPassed = true,
            NoMajorVisibleIssue = true,
            Result = PreTripCheckResult.PASS,
            CheckedAtUtc = DateTime.UtcNow
        };
        db.PreTripVehicleChecks.Add(check);
        await db.SaveChangesAsync();
        db.TripProtectionCoverages.Add(new TripProtectionCoverage
        {
            TripId = graph.Trip.Id,
            PolicyVersionId = graph.Policy.Id,
            PreTripVehicleCheckId = check.Id,
            ProtectionLimit = graph.Policy.DefaultProtectionLimit,
            ActivatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var ledger = new RiskFundLedgerService(db);
        var service = new TripFinancialSettlementService(
            db, new TripCommissionCalculator(), new RiskProtectionPolicyProvider(db), ledger);
        await service.CreateContributionForCompletedTripAsync(graph.Trip, CancellationToken.None);
        await service.CreateContributionForCompletedTripAsync(graph.Trip, CancellationToken.None);

        var settlement = await db.TripFinancialSettlements.SingleAsync();
        var contribution = await db.RiskFundTransactions.SingleAsync();
        Assert.Equal(30_000m, settlement.GrossPlatformCommission);
        Assert.Equal(70_000m, settlement.DriverEarning);
        Assert.Equal(10_000m, settlement.NetPlatformCommission);
        Assert.Equal(1_000m, settlement.RiskContribution);
        Assert.Equal(1_000m, contribution.Amount);
    }

    [Fact]
    public async Task ComponentAwareSettlement_PersistsBreakdownAndCreditsOneTotalDriverPayout()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(
            db, TripStatus.COMPLETED, riskEnabled: false, componentAwarePricing: true);
        graph.Trip.ActualFare = 100_000m;
        graph.Trip.EndReason = TripEndReason.NORMAL_COMPLETION;
        graph.Trip.Booking.BookingPromotions.Add(new BookingPromotion
        {
            BookingId = graph.Trip.BookingId,
            PromotionId = graph.Promotion.Id,
            DiscountAmount = 20_000m,
            CreatedAt = DateTime.UtcNow
        });
        db.BookingDriverOffers.Add(new BookingDriverOffer
        {
            BookingId = graph.Trip.BookingId,
            DriverId = graph.DriverId,
            OfferStatus = DriverOfferStatus.CustomerConfirmed,
            OfferedAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            ConfirmedAt = DateTime.UtcNow.AddHours(-1),
            PickupDistanceKm = 8m,
            LongPickupCompensation = 15_000m
        });
        await db.SaveChangesAsync();

        var service = new TripFinancialSettlementService(
            db,
            new TripCommissionCalculator(),
            new RiskProtectionPolicyProvider(db),
            new RiskFundLedgerService(db));
        await service.SettleQrDriverEarningAsync(graph.Trip, "phase4-test", CancellationToken.None);
        await service.SettleQrDriverEarningAsync(graph.Trip, "phase4-test", CancellationToken.None);

        var settlement = await db.TripFinancialSettlements.SingleAsync();
        Assert.Equal(TripFinancialSettlement.CurrentComponentBreakdownVersion, settlement.ComponentBreakdownVersion);
        Assert.Equal(100_000m, settlement.GrossFare);
        Assert.Equal(80_000m, settlement.FareComponent);
        Assert.Equal(20_000m, settlement.LongDistanceComponent);
        Assert.Equal(20_000m, settlement.AppliedPromotionDiscount);
        Assert.Equal(80_000m, settlement.CustomerPayableAmount);
        Assert.Equal(80_000m, settlement.CommissionBase);
        Assert.Equal(56_000m, settlement.DriverFareEarning);
        Assert.Equal(20_000m, settlement.LongDistanceEarning);
        Assert.Equal(15_000m, settlement.LongPickupCompensation);
        Assert.Equal(91_000m, settlement.DriverPayout);
        Assert.Equal(91_000m, settlement.DriverEarning);
        Assert.Equal(-11_000m, settlement.NetOperatingRevenue);
        var walletTransaction = await db.WalletTransactions.SingleAsync();
        Assert.Equal(91_000m, walletTransaction.Amount);
        Assert.Equal(91_000m, (await db.DriverWallets.SingleAsync()).CurrentBalance);
    }

    [Fact]
    public async Task ComponentAwareSettlement_EarlyStopWithLongDistanceComponent_ProratesComponentsAndPreservesPromotionPolicy()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(
            db, TripStatus.WAITING_PAYMENT, riskEnabled: false, componentAwarePricing: true);
        graph.Trip.ActualFare = 50_000m;
        graph.Trip.PlannedRouteProgress = 0.5m;
        graph.Trip.ActualDistanceKm = 999m;
        graph.Trip.EndReason = TripEndReason.CUSTOMER_REQUESTED_STOP;
        graph.Trip.Booking.BookingPromotions.Add(new BookingPromotion
        {
            BookingId = graph.Trip.BookingId,
            PromotionId = graph.Promotion.Id,
            DiscountAmount = 20_000m,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new TripFinancialSettlementService(
            db,
            new TripCommissionCalculator(),
            new RiskProtectionPolicyProvider(db),
            new RiskFundLedgerService(db));

        var settlement = await service.GetOrCreateAsync(
            graph.Trip,
            safetyTerminated: false,
            CancellationToken.None);

        Assert.Equal(50_000m, settlement.GrossFare);
        Assert.Equal(40_000m, settlement.FareComponent);
        Assert.Equal(10_000m, settlement.LongDistanceComponent);
        Assert.Equal(20_000m, settlement.SnapshotPromotionDiscount);
        Assert.Equal(20_000m, settlement.AppliedPromotionDiscount);
        Assert.Equal(30_000m, settlement.CustomerPayableAmount);
        Assert.Equal(40_000m, settlement.CommissionBase);
        Assert.Equal(12_000m, settlement.GrossPlatformCommission);
        Assert.Equal(28_000m, settlement.DriverFareEarning);
        Assert.Equal(10_000m, settlement.LongDistanceEarning);
        Assert.Equal(38_000m, settlement.DriverPayout);
        Assert.Equal(
            settlement.GrossFare,
            settlement.FareComponent + settlement.LongDistanceComponent);
    }

    [Theory]
    [InlineData(0d, 30_000, 30_000, 0)]
    [InlineData(0.1d, 30_000, 28_000, 2_000)]
    [InlineData(1d, 100_000, 80_000, 20_000)]
    public async Task ComponentAwareSettlement_EarlyStop_ReconcilesPersistedProgressComponents(
        double progress,
        decimal expectedGrossFare,
        decimal expectedFareComponent,
        decimal expectedLongDistanceComponent)
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(
            db, TripStatus.WAITING_PAYMENT, riskEnabled: false, componentAwarePricing: true);
        graph.Trip.ActualFare = expectedGrossFare;
        graph.Trip.PlannedRouteProgress = (decimal)progress;
        graph.Trip.EndReason = TripEndReason.CUSTOMER_REQUESTED_STOP;
        await db.SaveChangesAsync();
        var service = new TripFinancialSettlementService(
            db,
            new TripCommissionCalculator(),
            new RiskProtectionPolicyProvider(db),
            new RiskFundLedgerService(db));

        var settlement = await service.GetOrCreateAsync(
            graph.Trip,
            safetyTerminated: false,
            CancellationToken.None);

        Assert.Equal(expectedGrossFare, settlement.GrossFare);
        Assert.Equal(expectedFareComponent, settlement.FareComponent);
        Assert.Equal(expectedLongDistanceComponent, settlement.LongDistanceComponent);
        Assert.Equal(
            settlement.GrossFare,
            settlement.FareComponent + settlement.LongDistanceComponent);
    }

    [Fact]
    public async Task AdminRevenue_UsesSettledZeroPaymentTripWithoutLegacyPaymentRow()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(db, TripStatus.COMPLETED, riskEnabled: true);
        var settledAt = DateTime.UtcNow;
        db.TripFinancialSettlements.Add(new TripFinancialSettlement
        {
            TripId = graph.Trip.Id,
            PolicyVersionId = graph.Policy.Id,
            CommissionBase = 10_000m,
            PromotionExpense = 10_000m,
            CustomerPayableAmount = 0m,
            PlatformCommissionRate = .30m,
            GrossPlatformCommission = 3_000m,
            DriverEarning = 7_000m,
            NetPlatformCommission = -7_000m,
            RiskReserveRate = .10m,
            RiskContribution = 0m,
            NetOperatingRevenue = -7_000m,
            SettledAtUtc = settledAt,
            CreatedAtUtc = settledAt
        });
        await db.SaveChangesAsync();

        var service = new AdminRevenueQueryService(db, new TripCommissionCalculator());
        var today = DateOnly.FromDateTime(settledAt);
        var result = await service.GetAsync(today, today, CancellationToken.None);

        Assert.Equal(0m, result.TotalRevenue);
        Assert.Equal(1, result.SuccessfulTrips);
        Assert.Equal(-7_000m, result.PlatformRevenue);
        Assert.Empty(await db.Payments.ToListAsync());
    }

    [Fact]
    public async Task AdminRevenue_LegacyPromotedTrip_UsesCommissionBaseBeforePromotion()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(db, TripStatus.COMPLETED, riskEnabled: false);
        graph.Trip.ActualFare = 100_000m;
        graph.Trip.Booking.BookingPromotions.Add(new BookingPromotion
        {
            BookingId = graph.Trip.BookingId,
            PromotionId = graph.Promotion.Id,
            DiscountAmount = 40_000m,
            CreatedAt = DateTime.UtcNow
        });
        var settledAt = DateTime.UtcNow;
        db.Payments.Add(new Payment
        {
            TripId = graph.Trip.Id,
            PaymentMethod = PaymentMethod.CASH,
            PaymentStatus = PaymentStatus.Success,
            Amount = 60_000m,
            Currency = "VND",
            PaidAt = settledAt,
            CreatedAt = settledAt
        });
        await db.SaveChangesAsync();

        var service = new AdminRevenueQueryService(db, new TripCommissionCalculator());
        var today = DateOnly.FromDateTime(settledAt);
        var result = await service.GetAsync(today, today, CancellationToken.None);

        Assert.Equal(60_000m, result.TotalRevenue);
        Assert.Equal(-10_000m, result.PlatformRevenue);
        Assert.Equal(1, result.SuccessfulTrips);
    }

    [Fact]
    public async Task Accident_Create_AllowsParticipantsAndManagementButHidesFromOutsider()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(db, TripStatus.IN_PROGRESS, riskEnabled: true);
        graph.Trip.StartedAt = DateTime.UtcNow.AddMinutes(-30);
        await db.SaveChangesAsync();
        await ActivateCoverageAsync(db, graph);
        var realtime = new CapturingAccidentRealtimeService();
        var service = CreateAccidentService(db, realtime);

        var created = await service.CreateAsync(
            graph.DriverId,
            false,
            graph.Trip.Id,
            new CreateAccidentRequest(
                AccidentCategory.MULTIPLE,
                DateTime.UtcNow.AddMinutes(-5),
                10.776m,
                106.701m,
                "Collision during the trip",
                "POLICE-001"),
            CancellationToken.None);

        var customerView = await service.GetAsync(
            graph.Trip.Booking.CustomerId, false, created.Id, CancellationToken.None);
        var managementView = await service.GetAsync(
            Guid.NewGuid(), true, created.Id, CancellationToken.None);
        var outsiderError = await Assert.ThrowsAsync<BookingException>(() =>
            service.GetAsync(Guid.NewGuid(), false, created.Id, CancellationToken.None));
        var queue = await service.GetStaffQueueAsync(
            new AccidentQueueFilter(
                AccidentStatus.REPORTED, AccidentCategory.MULTIPLE, graph.Trip.Id,
                DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)),
            CancellationToken.None);

        Assert.Equal(created.Id, customerView.Id);
        Assert.Equal(created.Id, managementView.Id);
        Assert.Equal("accident.not_found", outsiderError.Code);
        Assert.Single(queue);
        Assert.Equal(created.Id, queue[0].Id);
        Assert.Equal(created.Id, (await db.Notifications.SingleAsync()).ReferenceId);
        Assert.Equal(created.Id, realtime.LastEvent?.AccidentId);
    }

    [Fact]
    public async Task Accident_Evidence_PersistsMetadataAndMovesToEvidenceCollection()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(db, TripStatus.IN_PROGRESS, riskEnabled: true);
        graph.Trip.StartedAt = DateTime.UtcNow.AddMinutes(-30);
        await db.SaveChangesAsync();
        await ActivateCoverageAsync(db, graph);
        var service = CreateAccidentService(db);
        var accident = await service.CreateAsync(
            graph.Trip.Booking.CustomerId,
            false,
            graph.Trip.Id,
            new CreateAccidentRequest(
                AccidentCategory.CUSTOMER_VEHICLE_DAMAGE,
                DateTime.UtcNow.AddMinutes(-3),
                null,
                null,
                "Vehicle body damage",
                null),
            CancellationToken.None);
        var capturedAt = DateTime.UtcNow.AddMinutes(-2);

        var evidence = await service.AddEvidenceAsync(
            graph.DriverId,
            false,
            accident.Id,
            new AddAccidentEvidenceRequest(
                AccidentEvidenceType.PHOTO,
                "https://evidence.test/accident/photo.jpg",
                "photo.jpg",
                "image/jpeg",
                "saferide/accident-evidence/test/photo",
                1024,
                capturedAt,
                10.776m,
                106.701m,
                "Front-left damage"),
            CancellationToken.None);
        var details = await service.GetAsync(
            graph.Trip.Booking.CustomerId, false, accident.Id, CancellationToken.None);

        Assert.Equal("photo.jpg", evidence.OriginalFileName);
        Assert.Equal(1024, evidence.FileSizeBytes);
        Assert.Equal(capturedAt, evidence.CapturedAtUtc);
        Assert.Equal(AccidentStatus.EVIDENCE_COLLECTION, details.Status);
        Assert.Single(details.Evidence!);
        Assert.Equal(evidence.Id, details.Evidence![0].Id);
    }

    [Fact]
    public async Task Accident_Evidence_RejectsOutsiderAndClosedAccident()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(db, TripStatus.IN_PROGRESS, riskEnabled: true);
        graph.Trip.StartedAt = DateTime.UtcNow.AddMinutes(-30);
        await db.SaveChangesAsync();
        await ActivateCoverageAsync(db, graph);
        var service = CreateAccidentService(db);
        var accident = await service.CreateAsync(
            graph.DriverId,
            false,
            graph.Trip.Id,
            new CreateAccidentRequest(
                AccidentCategory.DRIVER_INJURY,
                DateTime.UtcNow.AddMinutes(-2),
                null,
                null,
                "Minor driver injury",
                null),
            CancellationToken.None);

        var outsiderError = await Assert.ThrowsAsync<BookingException>(() =>
            service.EnsureCanUploadEvidenceAsync(
                Guid.NewGuid(), false, accident.Id, CancellationToken.None));
        var entity = await db.AccidentReports.SingleAsync(x => x.Id == accident.Id);
        entity.Status = AccidentStatus.CLOSED;
        await db.SaveChangesAsync();
        var closedError = await Assert.ThrowsAsync<BookingException>(() =>
            service.EnsureCanUploadEvidenceAsync(
                graph.DriverId, false, accident.Id, CancellationToken.None));
        entity.Status = AccidentStatus.REJECTED;
        await db.SaveChangesAsync();
        var rejectedError = await Assert.ThrowsAsync<BookingException>(() =>
            service.EnsureCanUploadEvidenceAsync(
                graph.DriverId, false, accident.Id, CancellationToken.None));

        Assert.Equal("accident.not_found", outsiderError.Code);
        Assert.Equal("risk_protection.conflict", closedError.Code);
        Assert.Equal("risk_protection.conflict", rejectedError.Code);
    }

    [Fact]
    public async Task Accident_Create_BeforeTripStartIsRejected()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(db, TripStatus.ARRIVED, riskEnabled: true);
        var service = CreateAccidentService(db);

        var exception = await Assert.ThrowsAsync<BookingException>(() => service.CreateAsync(
            graph.DriverId,
            false,
            graph.Trip.Id,
            new CreateAccidentRequest(
                AccidentCategory.MULTIPLE,
                DateTime.UtcNow,
                null,
                null,
                "Should not be accepted before trip start",
                null),
            CancellationToken.None));

        Assert.Equal("risk_protection.conflict", exception.Code);
        Assert.Empty(await db.AccidentReports.ToListAsync());
    }

    [Fact]
    public async Task Accident_Create_WithoutEligibleCoverageIsRejected()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(db, TripStatus.IN_PROGRESS, riskEnabled: true);
        graph.Trip.StartedAt = DateTime.UtcNow.AddMinutes(-10);
        await db.SaveChangesAsync();
        var service = CreateAccidentService(db);

        var exception = await Assert.ThrowsAsync<BookingException>(() => service.CreateAsync(
            graph.DriverId,
            false,
            graph.Trip.Id,
            new CreateAccidentRequest(
                AccidentCategory.MULTIPLE,
                DateTime.UtcNow.AddMinutes(-1),
                null,
                null,
                "No coverage",
                null),
            CancellationToken.None));

        Assert.Equal("risk_protection.conflict", exception.Code);
        Assert.Empty(await db.AccidentReports.ToListAsync());
    }

    [Fact]
    public async Task Accident_Create_RejectsOutsiderButAllowsManagementAndNotifiesBothParticipants()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(db, TripStatus.IN_PROGRESS, riskEnabled: true);
        graph.Trip.StartedAt = DateTime.UtcNow.AddMinutes(-10);
        await db.SaveChangesAsync();
        await ActivateCoverageAsync(db, graph);
        var service = CreateAccidentService(db);
        var request = new CreateAccidentRequest(
            AccidentCategory.MULTIPLE,
            DateTime.UtcNow.AddMinutes(-1),
            null,
            null,
            "Management report",
            null);

        var outsider = await Assert.ThrowsAsync<BookingException>(() =>
            service.CreateAsync(
                Guid.NewGuid(), false, graph.Trip.Id, request, CancellationToken.None));
        var created = await service.CreateAsync(
            Guid.NewGuid(), true, graph.Trip.Id, request, CancellationToken.None);

        Assert.Equal("trip.not_found", outsider.Code);
        Assert.True(created.Id > 0);
        var recipients = await db.Notifications.Select(x => x.UserId).ToListAsync();
        Assert.Equal(2, recipients.Distinct().Count());
        Assert.Contains(graph.DriverId, recipients);
        Assert.Contains(graph.Trip.Booking.CustomerId, recipients);
    }

    [Fact]
    public async Task SafetyReport_DistinguishesVehicleIssueAndCreatesDurableSosEscalation()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(db, TripStatus.IN_PROGRESS, riskEnabled: true);
        graph.Trip.StartedAt = DateTime.UtcNow.AddMinutes(-10);
        db.PreTripVehicleChecks.Add(new PreTripVehicleCheck
        {
            TripId = graph.Trip.Id,
            DriverId = graph.DriverId,
            BrakeResponsePassed = false,
            FrontRearLightsPassed = true,
            TurnSignalsPassed = true,
            VisibleTiresPassed = true,
            DashboardWarningPassed = true,
            WindshieldVisibilityPassed = true,
            NoMajorVisibleIssue = false,
            Result = PreTripCheckResult.FAIL,
            CheckedAtUtc = DateTime.UtcNow.AddMinutes(-11)
        });
        await db.SaveChangesAsync();
        var realtime = new CapturingAdminReportRealtimeService();
        var service = new SafetyReportService(
            db,
            realtime,
            NullLogger<SafetyReportService>.Instance);

        var result = await service.CreateAsync(
            graph.DriverId,
            graph.Trip.Id,
            new SafetyReportRequest(
                SafetyReportType.VEHICLE_ISSUE,
                "BRAKE_FAILURE",
                "Brake response became unsafe",
                10.776m,
                106.701m,
                true),
            CancellationToken.None);

        var report = await db.Reports.SingleAsync();
        var sos = await db.Sosalerts.SingleAsync();
        Assert.Equal(SafetyReportType.VEHICLE_ISSUE, report.ReportType);
        Assert.NotNull(report.PreTripVehicleCheckId);
        Assert.True(result.EscalationRequested);
        Assert.Equal(sos.Id, result.SosAlertId);
        Assert.Equal(graph.DriverId, sos.TriggeredByUserId);
        Assert.Equal(SOSStatus.Active, sos.SOSStatus);
        Assert.True(graph.Trip.IsSOSActivated);
        Assert.Equal(report.Id, realtime.LastEvent?.ReportId);
    }

    [Fact]
    public async Task SafetyReport_UnsafeCustomerDoesNotMasqueradeAsVehicleIssue()
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(db, TripStatus.IN_PROGRESS, riskEnabled: true);
        var service = new SafetyReportService(
            db,
            new CapturingAdminReportRealtimeService(),
            NullLogger<SafetyReportService>.Instance);

        var result = await service.CreateAsync(
            graph.DriverId,
            graph.Trip.Id,
            new SafetyReportRequest(
                SafetyReportType.UNSAFE_CUSTOMER,
                "VIOLENT",
                "Customer threatened the driver",
                null,
                null,
                false),
            CancellationToken.None);

        var report = await db.Reports.SingleAsync();
        Assert.Equal(SafetyReportType.UNSAFE_CUSTOMER, result.ReportType);
        Assert.Contains("unsafe customer", report.Subject, StringComparison.OrdinalIgnoreCase);
        Assert.Null(report.PreTripVehicleCheckId);
        Assert.Empty(await db.Sosalerts.ToListAsync());
    }

    [Theory]
    [InlineData(SafetyReportType.UNSAFE_CUSTOMER, "THREATENING_BEHAVIOR")]
    [InlineData(SafetyReportType.UNSAFE_CUSTOMER, "BRAKE_FAILURE")]
    [InlineData(SafetyReportType.VEHICLE_ISSUE, "UNSAFE_REQUEST")]
    public async Task SafetyReport_RejectsStaleOrCrossTypeReasonCodes(
        SafetyReportType reportType,
        string reasonCode)
    {
        await using var db = CreateDbContext();
        var graph = await SeedTripAsync(db, TripStatus.IN_PROGRESS, riskEnabled: true);
        var service = new SafetyReportService(
            db,
            new CapturingAdminReportRealtimeService(),
            NullLogger<SafetyReportService>.Instance);

        var exception = await Assert.ThrowsAsync<BookingException>(() => service.CreateAsync(
            graph.DriverId,
            graph.Trip.Id,
            new SafetyReportRequest(
                reportType,
                reasonCode,
                "Invalid reason contract",
                null,
                null,
                false),
            CancellationToken.None));

        Assert.Equal("safety_report.invalid_reason", exception.Code);
        Assert.Empty(await db.Reports.ToListAsync());
    }

    [Fact]
    public async Task LiabilityAssessment_RootCauseAllocationMustMatchEachResponsibleParty()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: false);
        var service = CreateAccidentService(db);

        var exception = await Assert.ThrowsAsync<BookingException>(() => service.SaveAssessmentAsync(
            Guid.NewGuid(),
            graph.Accident.Id,
            new LiabilityAssessmentRequest(
                40m,
                30m,
                10m,
                20m,
                0m,
                DriverFaultLevel.ORDINARY_NEGLIGENCE,
                VehicleDefectAwareness.UNKNOWN,
                new[]
                {
                    new LiabilityCauseRequest(AccidentRootCause.DRIVER_ERROR, ResponsiblePartyType.DRIVER, 30m),
                    new LiabilityCauseRequest(AccidentRootCause.CUSTOMER_INTERFERENCE, ResponsiblePartyType.CUSTOMER, 40m),
                    new LiabilityCauseRequest(AccidentRootCause.THIRD_PARTY_ERROR, ResponsiblePartyType.THIRD_PARTY, 10m),
                    new LiabilityCauseRequest(AccidentRootCause.VEHICLE_MECHANICAL_FAILURE, ResponsiblePartyType.VEHICLE, 20m)
                }),
            true,
            CancellationToken.None));

        Assert.Equal("risk_protection.invalid_request", exception.Code);
        Assert.Empty(await db.AccidentLiabilityAssessments.ToListAsync());
    }

    [Theory]
    [InlineData(ResponsiblePartyType.DRIVER, AccidentRootCause.DRIVER_ERROR)]
    [InlineData(ResponsiblePartyType.CUSTOMER, AccidentRootCause.CUSTOMER_INTERFERENCE)]
    [InlineData(ResponsiblePartyType.THIRD_PARTY, AccidentRootCause.THIRD_PARTY_ERROR)]
    [InlineData(ResponsiblePartyType.VEHICLE, AccidentRootCause.VEHICLE_MECHANICAL_FAILURE)]
    [InlineData(ResponsiblePartyType.OBJECTIVE, AccidentRootCause.ROAD_CONDITION)]
    public async Task LiabilityAssessment_AcceptsSinglePartyFaultScenarios(
        ResponsiblePartyType party,
        AccidentRootCause rootCause)
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: false);
        var service = CreateAccidentService(db);
        decimal Allocation(ResponsiblePartyType expected) => party == expected ? 100m : 0m;

        var result = await service.SaveAssessmentAsync(
            Guid.NewGuid(),
            graph.Accident.Id,
            new LiabilityAssessmentRequest(
                Allocation(ResponsiblePartyType.DRIVER),
                Allocation(ResponsiblePartyType.CUSTOMER),
                Allocation(ResponsiblePartyType.THIRD_PARTY),
                Allocation(ResponsiblePartyType.VEHICLE),
                Allocation(ResponsiblePartyType.OBJECTIVE),
                party == ResponsiblePartyType.DRIVER
                    ? DriverFaultLevel.INTENTIONAL_MISCONDUCT
                    : DriverFaultLevel.NO_FAULT,
                VehicleDefectAwareness.UNKNOWN,
                [new LiabilityCauseRequest(rootCause, party, 100m)]),
            true,
            CancellationToken.None);

        Assert.Equal(ProtectionClaimStatus.UNDER_REVIEW, result.Status);
    }

    [Fact]
    public void LiabilityAssessment_CustomerIntoxicationIsNotARootCause()
    {
        Assert.DoesNotContain("CUSTOMER_INTOXICATION", Enum.GetNames<AccidentRootCause>());
    }

    [Fact]
    public async Task LiabilityAssessment_HiddenDefectAndAwarenessMustMatchAllocation()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: false);
        var service = CreateAccidentService(db);
        var hiddenDefect = new LiabilityAssessmentRequest(
            0m, 0m, 0m, 100m, 0m,
            DriverFaultLevel.NO_FAULT,
            VehicleDefectAwareness.NEITHER_COULD_REASONABLY_KNOW,
            [new LiabilityCauseRequest(
                AccidentRootCause.VEHICLE_PRE_EXISTING_DEFECT,
                ResponsiblePartyType.VEHICLE,
                100m)]);

        var result = await service.SaveAssessmentAsync(
            Guid.NewGuid(), graph.Accident.Id, hiddenDefect, true, CancellationToken.None);
        Assert.Equal(ProtectionClaimStatus.UNDER_REVIEW, result.Status);

        await using var secondDb = CreateDbContext();
        var secondGraph = await SeedCoveredAccidentAsync(secondDb, withInsurance: false);
        var secondService = CreateAccidentService(secondDb);
        var inconsistent = hiddenDefect with { VehicleDefectAwareness = VehicleDefectAwareness.CUSTOMER_KNEW };
        var exception = await Assert.ThrowsAsync<BookingException>(() => secondService.SaveAssessmentAsync(
            Guid.NewGuid(), secondGraph.Accident.Id, inconsistent, true, CancellationToken.None));
        Assert.Equal("risk_protection.invalid_request", exception.Code);
    }

    [Fact]
    public async Task BusinessScenarioA_OrdinaryDriverNegligence_AppliesSnapshotRateAndCapWithoutWalletDeduction()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: false);
        var wallet = new DriverWallet
        {
            DriverId = graph.TripGraph.DriverId,
            CurrentBalance = 750_000m
        };
        db.DriverWallets.Add(wallet);
        await db.SaveChangesAsync();
        var service = CreateAccidentService(db);
        var claim = await service.SaveAssessmentAsync(
            Guid.NewGuid(),
            graph.Accident.Id,
            new LiabilityAssessmentRequest(
                100m, 0m, 0m, 0m, 0m,
                DriverFaultLevel.ORDINARY_NEGLIGENCE,
                VehicleDefectAwareness.UNKNOWN,
                [new LiabilityCauseRequest(
                    AccidentRootCause.DRIVER_ERROR,
                    ResponsiblePartyType.DRIVER,
                    100m)]),
            true,
            CancellationToken.None);

        var result = await service.CalculateClaimAsync(
            Guid.NewGuid(),
            claim.Id,
            new CalculateClaimRequest(
                50_000_000m,
                50_000_000m,
                0m,
                10_000_000m,
                false),
            CancellationToken.None);

        Assert.Equal(50_000_000m, (await db.DriverLiabilities.SingleAsync()).DriverAttributableEligibleDamage);
        Assert.Equal(2_000_000m, result.DriverLiabilityAmount);
        Assert.Equal(2_000_000m, result.RiskFundAdvanceAmount);
        Assert.Equal(18_000_000m, result.RiskFundPermanentLossAmount);
        Assert.Equal(750_000m, wallet.CurrentBalance);
        Assert.Empty(await db.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task BusinessScenarioB_CustomerInterference_CanBeRecoveredFromCustomerWithoutDriverLiability()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: false);
        var service = CreateAccidentService(db);
        var staffId = Guid.NewGuid();
        var claim = await service.SaveAssessmentAsync(
            staffId,
            graph.Accident.Id,
            new LiabilityAssessmentRequest(
                0m, 100m, 0m, 0m, 0m,
                DriverFaultLevel.NO_FAULT,
                VehicleDefectAwareness.UNKNOWN,
                [new LiabilityCauseRequest(
                    AccidentRootCause.CUSTOMER_INTERFERENCE,
                    ResponsiblePartyType.CUSTOMER,
                    100m)]),
            true,
            CancellationToken.None);
        var calculated = await service.CalculateClaimAsync(
            staffId,
            claim.Id,
            new CalculateClaimRequest(
                10_000_000m,
                10_000_000m,
                0m,
                10_000_000m,
                false,
                SubmitToInsurance: false),
            CancellationToken.None);

        Assert.Equal(0m, calculated.DriverLiabilityAmount);
        Assert.Equal(10_000_000m, calculated.CustomerLiabilityAmount);
        Assert.Equal(10_000_000m, calculated.RiskFundAdvanceAmount);
        await new RiskFundLedgerService(db).ApplyOpeningBalanceAsync(
            staffId,
            Mutation("scenario-b-opening") with { Amount = 10_000_000m },
            CancellationToken.None);
        await service.FundClaimAsync(staffId, claim.Id, "scenario-b-funding", CancellationToken.None);
        var recovered = await service.RecordRecoveryAsync(
            staffId,
            claim.Id,
            new ClaimRecoveryRequest(
                RecoverySourceType.CUSTOMER,
                graph.TripGraph.Trip.Booking.CustomerId.ToString(),
                10_000_000m,
                "CUSTOMER-SCENARIO-B",
                ClaimEvidence("https://evidence.test/scenario-b.pdf"),
                "scenario-b-recovery",
                string.Empty),
            CancellationToken.None);

        Assert.Equal(ProtectionClaimStatus.SETTLED, recovered.Status);
        Assert.Single(await db.RiskFundTransactions.Where(x =>
            x.TransactionType == RiskFundTransactionType.CUSTOMER_RECOVERY).ToListAsync());
        Assert.Empty(await db.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task BusinessScenarioC_PassiveCustomerIntoxication_CannotCreateFaultAndThirdPartyCanBeFullyResponsible()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: false);
        var service = CreateAccidentService(db);

        Assert.DoesNotContain("CUSTOMER_INTOXICATION", Enum.GetNames<AccidentRootCause>());
        var claim = await service.SaveAssessmentAsync(
            Guid.NewGuid(),
            graph.Accident.Id,
            new LiabilityAssessmentRequest(
                0m, 0m, 100m, 0m, 0m,
                DriverFaultLevel.NO_FAULT,
                VehicleDefectAwareness.UNKNOWN,
                [new LiabilityCauseRequest(
                    AccidentRootCause.THIRD_PARTY_ERROR,
                    ResponsiblePartyType.THIRD_PARTY,
                    100m)]),
            true,
            CancellationToken.None);
        var result = await service.CalculateClaimAsync(
            Guid.NewGuid(),
            claim.Id,
            new CalculateClaimRequest(10_000_000m, 10_000_000m, 0m, 0m, false),
            CancellationToken.None);

        Assert.Equal(0m, result.DriverLiabilityAmount);
        Assert.Equal(0m, result.CustomerLiabilityAmount);
        Assert.Equal(10_000_000m, result.ThirdPartyLiabilityAmount);
    }

    [Fact]
    public async Task BusinessScenarioD_ThirdPartyFault_RiskFundAdvanceIsRecoverableAndReplenishedExactlyOnce()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: false);
        var service = CreateAccidentService(db);
        var staffId = Guid.NewGuid();
        var claim = await service.SaveAssessmentAsync(
            staffId,
            graph.Accident.Id,
            new LiabilityAssessmentRequest(
                0m, 0m, 100m, 0m, 0m,
                DriverFaultLevel.NO_FAULT,
                VehicleDefectAwareness.UNKNOWN,
                [new LiabilityCauseRequest(
                    AccidentRootCause.THIRD_PARTY_ERROR,
                    ResponsiblePartyType.THIRD_PARTY,
                    100m)]),
            true,
            CancellationToken.None);
        var calculated = await service.CalculateClaimAsync(
            staffId,
            claim.Id,
            new CalculateClaimRequest(12_000_000m, 12_000_000m, 0m, 12_000_000m, false),
            CancellationToken.None);

        Assert.Equal(12_000_000m, calculated.RiskFundAdvanceAmount);
        Assert.Equal(0m, calculated.RiskFundPermanentLossAmount);
        await new RiskFundLedgerService(db).ApplyOpeningBalanceAsync(
            staffId,
            Mutation("scenario-d-opening") with { Amount = 12_000_000m },
            CancellationToken.None);
        await service.FundClaimAsync(staffId, claim.Id, "scenario-d-funding", CancellationToken.None);
        var request = new ClaimRecoveryRequest(
            RecoverySourceType.THIRD_PARTY,
            "THIRD-PARTY-SCENARIO-D",
            12_000_000m,
            "THIRD-PARTY-PAYMENT-D",
            ClaimEvidence("https://evidence.test/scenario-d.pdf"),
            "scenario-d-recovery",
            string.Empty);
        var recovered = await service.RecordRecoveryAsync(
            staffId, claim.Id, request, CancellationToken.None);
        var replay = await service.RecordRecoveryAsync(
            staffId, claim.Id, request, CancellationToken.None);

        Assert.Equal(recovered, replay);
        Assert.Equal(12_000_000m, (await db.RiskFundAccounts.SingleAsync()).CurrentBalance);
        Assert.Single(await db.RiskFundTransactions.Where(x =>
            x.TransactionType == RiskFundTransactionType.THIRD_PARTY_RECOVERY).ToListAsync());
    }

    [Fact]
    public async Task BusinessScenarioE_LatentBrakeDefect_AssignsVehicleWithoutInventingHumanFault()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: false);
        var service = CreateAccidentService(db);
        var claim = await service.SaveAssessmentAsync(
            Guid.NewGuid(),
            graph.Accident.Id,
            new LiabilityAssessmentRequest(
                0m, 0m, 0m, 100m, 0m,
                DriverFaultLevel.NO_FAULT,
                VehicleDefectAwareness.NEITHER_COULD_REASONABLY_KNOW,
                [new LiabilityCauseRequest(
                    AccidentRootCause.VEHICLE_PRE_EXISTING_DEFECT,
                    ResponsiblePartyType.VEHICLE,
                    100m)]),
            true,
            CancellationToken.None);
        var result = await service.CalculateClaimAsync(
            Guid.NewGuid(),
            claim.Id,
            new CalculateClaimRequest(10_000_000m, 10_000_000m, 0m, 10_000_000m, false),
            CancellationToken.None);

        Assert.Equal(0m, result.DriverLiabilityAmount);
        Assert.Equal(0m, result.CustomerLiabilityAmount);
        Assert.Equal(0m, result.ThirdPartyLiabilityAmount);
        Assert.Equal(0m, result.RiskFundAdvanceAmount);
        Assert.Equal(10_000_000m, result.RiskFundPermanentLossAmount);
    }

    [Fact]
    public async Task BusinessScenarioF_ConcealedKnownDefect_AssignsCustomerWithoutBlamingDriver()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: false);
        var service = CreateAccidentService(db);
        var claim = await service.SaveAssessmentAsync(
            Guid.NewGuid(),
            graph.Accident.Id,
            new LiabilityAssessmentRequest(
                0m, 100m, 0m, 0m, 0m,
                DriverFaultLevel.NO_FAULT,
                VehicleDefectAwareness.CUSTOMER_KNEW,
                [new LiabilityCauseRequest(
                    AccidentRootCause.VEHICLE_PRE_EXISTING_DEFECT,
                    ResponsiblePartyType.CUSTOMER,
                    100m)]),
            true,
            CancellationToken.None);
        var result = await service.CalculateClaimAsync(
            Guid.NewGuid(),
            claim.Id,
            new CalculateClaimRequest(10_000_000m, 10_000_000m, 0m, 10_000_000m, false),
            CancellationToken.None);

        Assert.Equal(0m, result.DriverLiabilityAmount);
        Assert.Equal(10_000_000m, result.CustomerLiabilityAmount);
        Assert.Equal(10_000_000m, result.RiskFundAdvanceAmount);
        Assert.Equal(10_000_000m, result.OutstandingRecoveryAmount);
        Assert.Empty(await db.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task ClaimCalculation_UsesConfirmedPercentagesAndIntentionalMisconductAttributableDamage()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: false);
        var service = CreateAccidentService(db);
        var assessment = ValidAssessment(DriverFaultLevel.INTENTIONAL_MISCONDUCT);
        var claim = await service.SaveAssessmentAsync(
            Guid.NewGuid(), graph.Accident.Id, assessment, true, CancellationToken.None);
        var participantView = await service.GetAsync(
            graph.TripGraph.DriverId, false, graph.Accident.Id, CancellationToken.None);

        Assert.NotNull(participantView.LiabilityAssessment);
        Assert.Equal(40m, participantView.LiabilityAssessment.DriverFaultPercentage);
        Assert.NotNull(participantView.Claim);
        Assert.Equal(claim.Id, participantView.Claim.Id);

        var result = await service.CalculateClaimAsync(
            Guid.NewGuid(),
            claim.Id,
            new CalculateClaimRequest(
                50_000_000m,
                40_000_000m,
                0m,
                16_000_000m,
                false,
                SubmitToInsurance: false),
            CancellationToken.None);

        Assert.Equal(16_000_000m, result.DriverLiabilityAmount);
        Assert.Equal(12_000_000m, result.CustomerLiabilityAmount);
        Assert.Equal(4_000_000m, result.ThirdPartyLiabilityAmount);
        Assert.Equal(20_000_000m, result.OutstandingRecoveryAmount);
        var refreshedParticipantView = await service.GetAsync(
            graph.TripGraph.DriverId, false, graph.Accident.Id, CancellationToken.None);
        Assert.Equal(40_000_000m, refreshedParticipantView.Claim?.EligibleDamageAmount);
        Assert.Equal(ProtectionClaimStatus.APPROVED, refreshedParticipantView.Claim?.Status);
        var liability = await db.DriverLiabilities.SingleAsync();
        Assert.Equal(16_000_000m, liability.DriverAttributableEligibleDamage);
        Assert.Equal(16_000_000m, liability.ConfirmedAmount);
    }

    [Fact]
    public async Task ClaimCalculation_AmountAboveRecoverableLiability_SplitsAdvanceFromFinalPayout()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: false);
        var service = CreateAccidentService(db);
        var claim = await service.SaveAssessmentAsync(
            Guid.NewGuid(), graph.Accident.Id, ValidAssessment(), true, CancellationToken.None);

        var result = await service.CalculateClaimAsync(
            Guid.NewGuid(),
            claim.Id,
            new CalculateClaimRequest(
                10_000_000m,
                10_000_000m,
                0m,
                10_000_000m,
                false),
            CancellationToken.None);

        var recoverableLiability = result.DriverLiabilityAmount
            + result.CustomerLiabilityAmount
            + result.ThirdPartyLiabilityAmount;
        Assert.Equal(recoverableLiability, result.RiskFundAdvanceAmount);
        Assert.Equal(10_000_000m - recoverableLiability, result.RiskFundPermanentLossAmount);
        Assert.Equal(recoverableLiability, result.OutstandingRecoveryAmount);
        Assert.True(result.IsReconciled);
    }

    [Fact]
    public async Task MockInsurance_PendingSubmissionAndStaffReviewAreAuditedWithoutOverpayingClaim()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: true);
        var service = CreateAccidentService(db);
        var claim = await service.SaveAssessmentAsync(
            Guid.NewGuid(), graph.Accident.Id, ValidAssessment(), true, CancellationToken.None);

        var calculated = await service.CalculateClaimAsync(
            Guid.NewGuid(),
            claim.Id,
            new CalculateClaimRequest(
                10_000_000m,
                10_000_000m,
                5_000_000m,
                3_000_000m,
                false),
            CancellationToken.None);

        Assert.Equal(InsuranceClaimStatus.PENDING, calculated.InsuranceStatus);
        Assert.Equal(ProtectionClaimStatus.UNDER_REVIEW, calculated.Status);
        Assert.Equal(7_000_000m, calculated.InsuranceRequestedAmount);
        Assert.Equal(0m, calculated.InsuranceApprovedAmount);
        Assert.Equal(0m, calculated.InsurancePaidDirectToClaimant);

        var refreshedStatus = await service.RefreshMockInsuranceStatusAsync(
            Guid.NewGuid(), claim.Id, calculated.RowVersion, CancellationToken.None);
        Assert.Equal(InsuranceClaimStatus.PENDING, refreshedStatus.InsuranceStatus);

        var reviewed = await service.ReviewMockInsuranceAsync(
            Guid.NewGuid(),
            claim.Id,
            new InsuranceReviewRequest(4_000_000m, "INS-APPROVED-01", "Reviewed evidence"),
            true,
            CancellationToken.None);

        Assert.Equal(InsuranceClaimStatus.APPROVED, reviewed.InsuranceStatus);
        Assert.Equal(ProtectionClaimStatus.APPROVED, reviewed.Status);
        Assert.Equal(4_000_000m, reviewed.InsuranceApprovedAmount);
        Assert.Equal(4_000_000m, reviewed.InsurancePaidDirectToClaimant);
        Assert.Equal(0m, reviewed.InsuranceReimbursedToRiskFund);
        Assert.Equal(4_000_000m, reviewed.TotalPaidToClaimant);
        Assert.True(reviewed.TotalPaidToClaimant
            + reviewed.RiskFundAdvanceAmount + reviewed.RiskFundPermanentLossAmount
            <= reviewed.EligibleDamageAmount);
        var audits = await service.GetInsuranceAuditsAsync(claim.Id, CancellationToken.None);
        Assert.Equal(4, audits.Count);
        Assert.Equal(InsuranceProviderOperation.CALCULATE, audits[0].Operation);
        Assert.Equal(7_000_000m, audits[0].ApprovedAmount);
        Assert.Equal(InsuranceProviderOperation.SUBMIT, audits[1].Operation);
        Assert.Equal(InsuranceClaimStatus.PENDING, audits[1].ResultStatus);
        Assert.Equal(7_000_000m, audits[1].ApprovedAmount);
        Assert.Equal(InsuranceProviderOperation.GET_STATUS, audits[2].Operation);
        Assert.Equal(InsuranceProviderOperation.APPROVE, audits[3].Operation);
        Assert.Equal(4_000_000m, audits[3].ApprovedAmount);
    }

    [Fact]
    public async Task MockInsuranceReview_EnforcesMaximumReasonAndSingleFinalTransition()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: true);
        var staffId = Guid.NewGuid();
        var service = CreateAccidentService(db);
        var claim = await service.SaveAssessmentAsync(staffId, graph.Accident.Id, ValidAssessment(), true, CancellationToken.None);
        var pending = await service.CalculateClaimAsync(staffId, claim.Id,
            new CalculateClaimRequest(10_000_000m, 10_000_000m), CancellationToken.None);

        Assert.Equal(7_000_000m, pending.MaximumApprovableInsuranceAmount);
        await Assert.ThrowsAsync<BookingException>(() => service.ReviewMockInsuranceAsync(staffId, claim.Id,
            new InsuranceReviewRequest(7_000_001m, null, "Above provider maximum", pending.RowVersion), true, CancellationToken.None));
        await Assert.ThrowsAsync<BookingException>(() => service.ReviewMockInsuranceAsync(staffId, claim.Id,
            new InsuranceReviewRequest(1_000_000m, null, null, pending.RowVersion), true, CancellationToken.None));
        await Assert.ThrowsAsync<BookingException>(() => service.ReviewMockInsuranceAsync(staffId, claim.Id,
            new InsuranceReviewRequest(1m, null, "Reject with amount", pending.RowVersion), false, CancellationToken.None));

        var reviewed = await service.ReviewMockInsuranceAsync(staffId, claim.Id,
            new InsuranceReviewRequest(4_000_000m, null, "Partial damage outside policy scope", pending.RowVersion), true, CancellationToken.None);
        Assert.StartsWith("MOCK-", reviewed.InsuranceReference ?? string.Empty);
        await Assert.ThrowsAsync<BookingException>(() => service.ReviewMockInsuranceAsync(staffId, claim.Id,
            new InsuranceReviewRequest(4_000_000m, null, "Repeated review", reviewed.RowVersion), true, CancellationToken.None));
    }

    [Fact]
    public async Task InsurancePrioritySettlement_AppliesCustomerInsuranceThenAllocatesSystemInsurance()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: true);
        var staffId = Guid.NewGuid();
        var service = CreateAccidentService(db);
        var claim = await service.SaveAssessmentAsync(
            staffId,
            graph.Accident.Id,
            new LiabilityAssessmentRequest(
                30m, 70m, 0m, 0m, 0m,
                DriverFaultLevel.ORDINARY_NEGLIGENCE,
                VehicleDefectAwareness.UNKNOWN,
                [
                    new LiabilityCauseRequest(AccidentRootCause.DRIVER_ERROR, ResponsiblePartyType.DRIVER, 30m),
                    new LiabilityCauseRequest(AccidentRootCause.CUSTOMER_INTERFERENCE, ResponsiblePartyType.CUSTOMER, 70m)
                ]),
            true,
            CancellationToken.None);

        var pending = await service.CalculateClaimAsync(
            staffId,
            claim.Id,
            new CalculateClaimRequest(
                10_000_000m,
                10_000_000m,
                CustomerInsuranceAppliedAmount: 6_000_000m,
                CustomerInsuranceReference: "CUSTOMER-EXT-001",
                CustomerInsuranceNote: "Confirmed external contribution"),
            CancellationToken.None);
        Assert.Equal(InsuranceClaimStatus.PENDING, pending.InsuranceStatus);
        Assert.Equal(7_000_000m, pending.CustomerGrossExposure);
        Assert.Equal(3_000_000m, pending.DriverGrossExposure);
        Assert.Equal(6_000_000m, pending.CustomerInsuranceAppliedAmount);
        Assert.Equal("CUSTOMER-EXT-001", pending.CustomerInsuranceReference);
        Assert.NotNull(pending.CustomerInsuranceConfirmedAtUtc);
        Assert.Equal("Confirmed external contribution", pending.CustomerInsuranceNote);
        Assert.Equal(1_000_000m, pending.CustomerExposureAfterOwnInsurance);
        Assert.Equal(4_000_000m, pending.SystemInsuranceMaximumAmount);
        Assert.Equal(4_000_000m, pending.ResidualUninsuredDamage);

        var reviewed = await service.ReviewMockInsuranceAsync(
            staffId,
            claim.Id,
            new InsuranceReviewRequest(2_000_000m, "A3-APPROVAL", "Approved lower than recommendation"),
            true,
            CancellationToken.None);

        Assert.Equal(2_000_000m, reviewed.SystemInsuranceApprovedAmount);
        Assert.Equal(500_000m, reviewed.CustomerSystemInsuranceBenefit);
        Assert.Equal(1_500_000m, reviewed.DriverSystemInsuranceBenefit);
        Assert.Equal(2_000_000m, reviewed.ResidualUninsuredDamage);
        Assert.Equal(1_500_000m, reviewed.DriverRemainingExposureBeforeRateCap);
        Assert.Equal(300_000m, reviewed.DriverLiabilityAmount);
        Assert.Equal(500_000m, reviewed.CustomerFinalExposure);
        Assert.Equal(500_000m, reviewed.CustomerLiabilityAmount);
        Assert.Equal(0m, reviewed.ThirdPartyLiabilityAmount);
        Assert.Equal(2_000_000m, reviewed.RiskFundRequiredAmount);
        Assert.Equal(10_000_000m,
            reviewed.CustomerInsuranceAppliedAmount
                + reviewed.SystemInsuranceApprovedAmount
                + reviewed.RiskFundRequiredAmount);
    }

    [Fact]
    public async Task CustomerInsurance_AboveCustomerGrossExposure_IsRejectedByServer()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: false);
        var staffId = Guid.NewGuid();
        var service = CreateAccidentService(db);
        var claim = await service.SaveAssessmentAsync(
            staffId,
            graph.Accident.Id,
            new LiabilityAssessmentRequest(
                30m, 70m, 0m, 0m, 0m,
                DriverFaultLevel.ORDINARY_NEGLIGENCE,
                VehicleDefectAwareness.UNKNOWN,
                [
                    new LiabilityCauseRequest(AccidentRootCause.DRIVER_ERROR, ResponsiblePartyType.DRIVER, 30m),
                    new LiabilityCauseRequest(AccidentRootCause.CUSTOMER_INTERFERENCE, ResponsiblePartyType.CUSTOMER, 70m)
                ]),
            true,
            CancellationToken.None);

        var exception = await Assert.ThrowsAsync<BookingException>(() => service.CalculateClaimAsync(
            staffId,
            claim.Id,
            new CalculateClaimRequest(
                10_000_000m,
                10_000_000m,
                CustomerInsuranceAppliedAmount: 7_000_001m),
            CancellationToken.None));

        Assert.Equal("risk_protection.invalid_request", exception.Code);
    }

    [Fact]
    public async Task SystemInsurance_IsAvailableWithoutLegacyCustomerPhysicalDamageSnapshot()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: false);
        var staffId = Guid.NewGuid();
        var service = CreateAccidentService(db);
        var claim = await service.SaveAssessmentAsync(
            staffId,
            graph.Accident.Id,
            new LiabilityAssessmentRequest(
                30m, 70m, 0m, 0m, 0m,
                DriverFaultLevel.ORDINARY_NEGLIGENCE,
                VehicleDefectAwareness.UNKNOWN,
                [
                    new LiabilityCauseRequest(AccidentRootCause.DRIVER_ERROR, ResponsiblePartyType.DRIVER, 30m),
                    new LiabilityCauseRequest(AccidentRootCause.CUSTOMER_INTERFERENCE, ResponsiblePartyType.CUSTOMER, 70m)
                ]),
            true,
            CancellationToken.None);

        var result = await service.CalculateClaimAsync(
            staffId,
            claim.Id,
            new CalculateClaimRequest(10_000_000m, 10_000_000m),
            CancellationToken.None);

        Assert.Null(graph.Coverage.InsuranceCoverageSnapshot);
        Assert.Equal(10_000_000m, result.SystemInsuranceCoverageLimitSnapshot);
        Assert.Equal(10_000_000m, result.SystemInsuranceMaximumAmount);
        Assert.Equal(InsuranceClaimStatus.PENDING, result.InsuranceStatus);
    }

    [Fact]
    public async Task SystemInsurance_UsesTripPolicyVersionAfterNewCurrentPolicyIsCreated()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: false);
        db.RiskProtectionPolicyVersions.Add(new RiskProtectionPolicyVersion
        {
            EffectiveFromUtc = DateTime.UtcNow,
            MockInsuranceCoverageLimit = 1m,
            ChangeReason = "New policy must not mutate historical coverage"
        });
        await db.SaveChangesAsync();
        var staffId = Guid.NewGuid();
        var service = CreateAccidentService(db);
        var claim = await service.SaveAssessmentAsync(
            staffId,
            graph.Accident.Id,
            new LiabilityAssessmentRequest(
                30m, 70m, 0m, 0m, 0m,
                DriverFaultLevel.ORDINARY_NEGLIGENCE,
                VehicleDefectAwareness.UNKNOWN,
                [
                    new LiabilityCauseRequest(AccidentRootCause.DRIVER_ERROR, ResponsiblePartyType.DRIVER, 30m),
                    new LiabilityCauseRequest(AccidentRootCause.CUSTOMER_INTERFERENCE, ResponsiblePartyType.CUSTOMER, 70m)
                ]),
            true,
            CancellationToken.None);

        var result = await service.CalculateClaimAsync(
            staffId,
            claim.Id,
            new CalculateClaimRequest(10_000_000m, 10_000_000m),
            CancellationToken.None);

        Assert.Equal(graph.TripGraph.Policy.Id, graph.Coverage.PolicyVersionId);
        Assert.Equal(10_000_000m, result.SystemInsuranceCoverageLimitSnapshot);
        Assert.Equal(10_000_000m, result.SystemInsuranceMaximumAmount);
        var reloaded = await service.GetAsync(
            staffId, true, graph.Accident.Id, CancellationToken.None);
        Assert.Equal(10_000_000m, reloaded.Claim?.SystemInsuranceCoverageLimitSnapshot);
        Assert.Equal(10_000_000m, reloaded.Claim?.SystemInsuranceMaximumAmount);
    }

    [Fact]
    public async Task InsuranceFirstSettlement_FullApprovalLeavesFaultHistoryButNoFinancialExposure()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: true);
        var staffId = Guid.NewGuid();
        var service = CreateAccidentService(db);
        var claim = await service.SaveAssessmentAsync(
            staffId,
            graph.Accident.Id,
            new LiabilityAssessmentRequest(
                30m, 70m, 0m, 0m, 0m,
                DriverFaultLevel.ORDINARY_NEGLIGENCE,
                VehicleDefectAwareness.UNKNOWN,
                [
                    new LiabilityCauseRequest(AccidentRootCause.DRIVER_ERROR, ResponsiblePartyType.DRIVER, 30m),
                    new LiabilityCauseRequest(AccidentRootCause.CUSTOMER_INTERFERENCE, ResponsiblePartyType.CUSTOMER, 70m)
                ]),
            true,
            CancellationToken.None);
        await service.CalculateClaimAsync(
            staffId,
            claim.Id,
            new CalculateClaimRequest(9_000_000m, 9_000_000m),
            CancellationToken.None);

        var reviewed = await service.ReviewMockInsuranceAsync(
            staffId,
            claim.Id,
            new InsuranceReviewRequest(9_000_000m, "A1-FULL-APPROVAL", "Full eligible loss approved"),
            true,
            CancellationToken.None);

        Assert.Equal(0m, reviewed.ResidualUninsuredDamage);
        Assert.Equal(0m, reviewed.DriverAttributableResidualDamage);
        Assert.Equal(0m, reviewed.DriverLiabilityAmount);
        Assert.Equal(0m, reviewed.CustomerLiabilityAmount);
        Assert.Equal(0m, reviewed.ThirdPartyLiabilityAmount);
        Assert.Equal(0m, reviewed.RiskFundRequiredAmount);
        var assessment = await db.AccidentLiabilityAssessments.SingleAsync();
        Assert.Equal(30m, assessment.DriverFaultPercentage);
        Assert.Equal(70m, assessment.CustomerFaultPercentage);
        Assert.Equal(LiabilityAssessmentStatus.CONFIRMED, assessment.Status);
    }

    [Theory]
    [InlineData(
        ResponsiblePartyType.VEHICLE,
        AccidentRootCause.VEHICLE_PRE_EXISTING_DEFECT,
        VehicleDefectAwareness.NEITHER_COULD_REASONABLY_KNOW)]
    [InlineData(
        ResponsiblePartyType.OBJECTIVE,
        AccidentRootCause.ROAD_CONDITION,
        VehicleDefectAwareness.UNKNOWN)]
    public async Task InsuranceFirstSettlement_NonHumanResidualBecomesPermanentProtectionLoss(
        ResponsiblePartyType party,
        AccidentRootCause rootCause,
        VehicleDefectAwareness awareness)
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: true);
        var staffId = Guid.NewGuid();
        var service = CreateAccidentService(db);
        decimal Allocation(ResponsiblePartyType expected) => party == expected ? 100m : 0m;
        var claim = await service.SaveAssessmentAsync(
            staffId,
            graph.Accident.Id,
            new LiabilityAssessmentRequest(
                0m,
                0m,
                0m,
                Allocation(ResponsiblePartyType.VEHICLE),
                Allocation(ResponsiblePartyType.OBJECTIVE),
                DriverFaultLevel.NO_FAULT,
                awareness,
                [new LiabilityCauseRequest(rootCause, party, 100m)]),
            true,
            CancellationToken.None);
        var calculated = await service.CalculateClaimAsync(
            staffId,
            claim.Id,
            new CalculateClaimRequest(10_000_000m, 10_000_000m),
            CancellationToken.None);

        Assert.Equal(InsuranceClaimStatus.NOT_SUBMITTED, calculated.InsuranceStatus);
        Assert.Equal(0m, calculated.SystemInsuranceMaximumAmount);
        Assert.Equal(10_000_000m, calculated.ResidualUninsuredDamage);
        Assert.Equal(10_000_000m, calculated.VehicleObjectiveResidualAmount);
        Assert.Equal(0m, calculated.DriverLiabilityAmount);
        Assert.Equal(0m, calculated.CustomerLiabilityAmount);
        Assert.Equal(0m, calculated.ThirdPartyLiabilityAmount);
        Assert.Equal(0m, calculated.RiskFundAdvanceAmount);
        Assert.Equal(10_000_000m, calculated.RiskFundPermanentLossAmount);
    }

    [Fact]
    public async Task SystemInsurance_DoesNotCollapseThirdPartyRecoveryAccounting()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: true);
        var staffId = Guid.NewGuid();
        var service = CreateAccidentService(db);
        var claim = await service.SaveAssessmentAsync(
            staffId,
            graph.Accident.Id,
            new LiabilityAssessmentRequest(
                0m, 0m, 100m, 0m, 0m,
                DriverFaultLevel.NO_FAULT,
                VehicleDefectAwareness.UNKNOWN,
                [new LiabilityCauseRequest(
                    AccidentRootCause.THIRD_PARTY_ERROR,
                    ResponsiblePartyType.THIRD_PARTY,
                    100m)]),
            true,
            CancellationToken.None);
        var pending = await service.CalculateClaimAsync(
            staffId,
            claim.Id,
            new CalculateClaimRequest(10_000_000m, 10_000_000m),
            CancellationToken.None);

        Assert.Equal(InsuranceClaimStatus.NOT_SUBMITTED, pending.InsuranceStatus);
        Assert.Equal(0m, pending.SystemInsuranceMaximumAmount);
        Assert.Equal(0m, pending.InsuranceApprovedAmount);
        Assert.Equal(10_000_000m, pending.ResidualUninsuredDamage);
        Assert.Equal(10_000_000m, pending.ThirdPartyLiabilityAmount);
        Assert.Equal(10_000_000m, pending.RiskFundAdvanceAmount);
        Assert.Equal(0m, pending.RiskFundPermanentLossAmount);
        Assert.Equal(ProtectionClaimStatus.APPROVED, pending.Status);
    }

    [Fact]
    public async Task InsuranceReimbursement_IsSeparateFromDirectPaymentAndCannotDoubleRecover()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: true);
        var service = CreateAccidentService(db);
        var staffId = Guid.NewGuid();
        var claim = await service.SaveAssessmentAsync(
            staffId, graph.Accident.Id, ValidAssessment(), true, CancellationToken.None);
        var calculated = await service.CalculateClaimAsync(
            staffId,
            claim.Id,
            new CalculateClaimRequest(
                10_000_000m,
                10_000_000m,
                5_000_000m,
                10_000_000m,
                false,
                InsurancePaymentDestination: InsurancePaymentDestination.REIMBURSE_RISK_FUND),
            CancellationToken.None);
        var reviewed = await service.ReviewMockInsuranceAsync(
            staffId,
            claim.Id,
            new InsuranceReviewRequest(
                4_000_000m,
                "INS-FUND-01",
                "Risk Fund advanced claimant payment",
                InsurancePaymentDestination: InsurancePaymentDestination.REIMBURSE_RISK_FUND),
            true,
            CancellationToken.None);

        Assert.Equal(4_000_000m, reviewed.InsuranceApprovedAmount);
        Assert.Equal(0m, reviewed.InsurancePaidDirectToClaimant);
        Assert.Equal(0m, reviewed.InsuranceReimbursedToRiskFund);
        Assert.Equal(0m, reviewed.TotalPaidToClaimant);

        await new RiskFundLedgerService(db).ApplyOpeningBalanceAsync(
            staffId,
            Mutation("insurance-reimbursement-opening") with { Amount = 10_000_000m },
            CancellationToken.None);
        var funded = await service.FundClaimAsync(
            staffId, claim.Id, "insurance-reimbursement-funding", CancellationToken.None);
        Assert.Equal(10_000_000m, funded.TotalPaidToClaimant);

        var reimbursed = await service.RecordRecoveryAsync(
            staffId,
            claim.Id,
            new ClaimRecoveryRequest(
                RecoverySourceType.INSURANCE,
                "MOCK-INSURER",
                4_000_000m,
                "INS-FUND-01",
                ClaimEvidence("https://evidence.test/insurance-reimbursement.pdf"),
                "insurance-reimbursement-01",
                string.Empty),
            CancellationToken.None);
        Assert.Equal(4_000_000m, reimbursed.InsuranceReimbursedToRiskFund);
        Assert.Equal(0m, reimbursed.InsurancePaidDirectToClaimant);
        Assert.Equal(10_000_000m, reimbursed.TotalPaidToClaimant);

        var exception = await Assert.ThrowsAsync<BookingException>(() => service.RecordRecoveryAsync(
            staffId,
            claim.Id,
            new ClaimRecoveryRequest(
                RecoverySourceType.INSURANCE,
                "MOCK-INSURER",
                1m,
                "INS-FUND-02",
                ClaimEvidence("https://evidence.test/insurance-reimbursement-duplicate.pdf"),
                "insurance-reimbursement-02",
                string.Empty),
            CancellationToken.None));
        Assert.Equal("risk_protection.recovery_exceeds_outstanding", exception.Code);
    }

    [Fact]
    public async Task ClaimCalculation_IgnoresManualSourceAmountsAndKeepsServerFundingBalanced()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: true);
        var service = CreateAccidentService(db);
        var claim = await service.SaveAssessmentAsync(
            Guid.NewGuid(), graph.Accident.Id, ValidAssessment(), true, CancellationToken.None);

        var result = await service.CalculateClaimAsync(
            Guid.NewGuid(),
            claim.Id,
            new CalculateClaimRequest(
                10_000_000m,
                10_000_000m,
                5_000_000m,
                6_000_000m,
                false),
            CancellationToken.None);

        Assert.Equal(7_000_000m, result.InsuranceEligibleAmount);
        Assert.Equal(10_000_000m, result.ResidualUninsuredDamage);
        Assert.Equal(10_000_000m, result.RiskFundRequiredAmount);
        Assert.True(result.InsurancePaidDirectToClaimant + result.RiskFundRequiredAmount
            <= result.EligibleDamageAmount);
    }

    [Fact]
    public async Task LiabilityDispute_ReopensClaimAndMarksDriverLiabilityDisputedBeforeFunding()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: false);
        var service = CreateAccidentService(db);
        var disputeEvidence = await service.AddEvidenceAsync(
            graph.TripGraph.DriverId,
            false,
            graph.Accident.Id,
            new AddAccidentEvidenceRequest(
                AccidentEvidenceType.DRIVER_STATEMENT,
                "https://evidence.test/accident/dispute.pdf",
                "dispute.pdf",
                "application/pdf",
                "saferide/accident-evidence/dispute",
                1024,
                DateTime.UtcNow,
                null,
                null,
                "Driver dispute evidence"),
            CancellationToken.None);
        var claim = await service.SaveAssessmentAsync(
            Guid.NewGuid(), graph.Accident.Id, ValidAssessment(), true, CancellationToken.None);
        await service.CalculateClaimAsync(
            Guid.NewGuid(),
            claim.Id,
            new CalculateClaimRequest(10_000_000m, 10_000_000m, 0m, 2_000_000m, false),
            CancellationToken.None);

        await service.DisputeLiabilityAsync(
            graph.TripGraph.DriverId,
            graph.Accident.Id,
            new LiabilityDisputeRequest(
                "Driver requests evidence review",
                [disputeEvidence.Id]),
            CancellationToken.None);

        var storedClaim = await db.ProtectionClaims.SingleAsync();
        var storedLiability = await db.DriverLiabilities.SingleAsync();
        Assert.Equal(ProtectionClaimStatus.UNDER_REVIEW, storedClaim.Status);
        Assert.Equal(DriverLiabilityStatus.DISPUTED, storedLiability.Status);
        Assert.Equal("Driver requests evidence review", storedLiability.DisputeReason);
        var audit = await db.LiabilityDisputeAudits
            .Include(x => x.Evidence)
            .SingleAsync();
        Assert.Equal(graph.TripGraph.DriverId, audit.DisputedByUserId);
        Assert.Equal("Driver requests evidence review", audit.Reason);
        Assert.Equal(disputeEvidence.Id, Assert.Single(audit.Evidence).AccidentEvidenceId);
    }

    [Fact]
    public async Task LiabilityDispute_AfterFundingIsRejectedWithoutAuditMutation()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: false);
        var service = CreateAccidentService(db);
        var evidence = await service.AddEvidenceAsync(
            graph.TripGraph.DriverId,
            false,
            graph.Accident.Id,
            new AddAccidentEvidenceRequest(
                AccidentEvidenceType.DRIVER_STATEMENT,
                "https://evidence.test/funded-dispute.pdf",
                "funded-dispute.pdf",
                "application/pdf",
                "saferide/accident-evidence/funded-dispute",
                1024,
                DateTime.UtcNow,
                null,
                null,
                null),
            CancellationToken.None);
        var claim = await service.SaveAssessmentAsync(
            Guid.NewGuid(), graph.Accident.Id, ValidAssessment(), true, CancellationToken.None);
        await service.CalculateClaimAsync(
            Guid.NewGuid(),
            claim.Id,
            new CalculateClaimRequest(100_000m, 100_000m, 0m, 100_000m, false),
            CancellationToken.None);
        await new RiskFundLedgerService(db).ApplyOpeningBalanceAsync(
            Guid.NewGuid(),
            Mutation("funded-dispute-opening") with { Amount = 100_000m },
            CancellationToken.None);
        await service.FundClaimAsync(
            Guid.NewGuid(), claim.Id, "funded-dispute-funding", CancellationToken.None);

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            service.DisputeLiabilityAsync(
                graph.TripGraph.DriverId,
                graph.Accident.Id,
                new LiabilityDisputeRequest("Too late", [evidence.Id]),
                CancellationToken.None));

        Assert.Equal("risk_protection.conflict", exception.Code);
        Assert.Empty(await db.LiabilityDisputeAudits.ToListAsync());
        Assert.Equal(
            LiabilityAssessmentStatus.CONFIRMED,
            (await db.AccidentLiabilityAssessments.SingleAsync()).Status);
    }

    [Fact]
    public async Task ClaimFunding_InsufficientBalanceMovesPendingAndManualRetryFundsWholeAmountOnce()
    {
        await using var db = CreateDbContext();
        var graph = await SeedFundableClaimAsync(db, 100_000m);
        var ledger = new RiskFundLedgerService(db);
        var service = CreateAccidentService(db);
        var staffId = Guid.NewGuid();
        await ledger.ApplyOpeningBalanceAsync(
            staffId,
            Mutation("phase7-opening") with { Amount = 50_000m },
            CancellationToken.None);

        var pending = await service.FundClaimAsync(
            staffId, graph.Claim.Id, " phase7-funding ", CancellationToken.None);
        var notificationCount = await db.Notifications.CountAsync();

        Assert.Equal(ProtectionClaimStatus.PENDING_FUNDING, pending.Status);
        Assert.Equal(50_000m, (await db.RiskFundAccounts.SingleAsync()).CurrentBalance);
        Assert.DoesNotContain(
            await db.RiskFundTransactions.ToListAsync(),
            transaction => transaction.TransactionType == RiskFundTransactionType.CLAIM_ADVANCE);

        await service.FundClaimAsync(
            staffId, graph.Claim.Id, "phase7-funding", CancellationToken.None);
        Assert.Equal(notificationCount, await db.Notifications.CountAsync());

        await ledger.ApplyAdjustmentAsync(
            staffId,
            new RiskFundMutationRequest(
                100_000m,
                LedgerDirection.CREDIT,
                "Add audited funding capacity",
                "BANK-PHASE7",
                "https://evidence.test/phase7-adjustment.pdf",
                "phase7-adjustment"),
            CancellationToken.None);

        var funded = await service.FundClaimAsync(
            staffId, graph.Claim.Id, " phase7-funding ", CancellationToken.None);
        var replay = await service.FundClaimAsync(
            staffId, graph.Claim.Id, "phase7-funding", CancellationToken.None);
        var aggregateReplay = await service.FundClaimAsync(
            staffId, graph.Claim.Id, "phase7-funding-new-key", CancellationToken.None);

        Assert.Equal(ProtectionClaimStatus.RECOVERY_IN_PROGRESS, funded.Status);
        Assert.Equal(100_000m, funded.TotalPaidToClaimant);
        Assert.Equal(funded, replay);
        Assert.Equal(funded, aggregateReplay);
        Assert.Equal(50_000m, (await db.RiskFundAccounts.SingleAsync()).CurrentBalance);
        var advances = await db.RiskFundTransactions
            .Where(transaction => transaction.TransactionType == RiskFundTransactionType.CLAIM_ADVANCE)
            .ToListAsync();
        Assert.Single(advances);
        Assert.Equal(100_000m, advances[0].Amount);
    }

    [Fact]
    public async Task ManualRecovery_IsAuditedIdempotentAndNeverDeductsDriverWallet()
    {
        await using var db = CreateDbContext();
        var graph = await SeedFundableClaimAsync(db, 100_000m);
        var ledger = new RiskFundLedgerService(db);
        var service = CreateAccidentService(db);
        var staffId = Guid.NewGuid();
        await ledger.ApplyOpeningBalanceAsync(
            staffId,
            Mutation("recovery-opening") with { Amount = 100_000m },
            CancellationToken.None);
        await service.FundClaimAsync(
            staffId, graph.Claim.Id, "recovery-funding", CancellationToken.None);
        var request = new ClaimRecoveryRequest(
            RecoverySourceType.DRIVER,
            graph.DriverId.ToString(),
            40_000m,
            " DRIVER-PAYMENT-1234 ",
            ClaimEvidence(" https://evidence.test/driver-recovery.pdf "),
            " recovery-001 ",
            string.Empty);

        var first = await service.RecordRecoveryAsync(
            staffId, graph.Claim.Id, request, CancellationToken.None);
        var replay = await service.RecordRecoveryAsync(
            staffId, graph.Claim.Id, request, CancellationToken.None);

        Assert.Equal(first, replay);
        Assert.Equal(40_000m, first.RecoveredAmount);
        Assert.Equal(60_000m, first.OutstandingRecoveryAmount);
        Assert.Equal(100_000m, first.TotalPaidToClaimant);
        Assert.Single(await db.ClaimRecoveries.ToListAsync());
        var recoveryTransactions = await db.RiskFundTransactions.Where(
            transaction => transaction.TransactionType == RiskFundTransactionType.DRIVER_RECOVERY)
            .ToListAsync();
        var recoveryTransaction = Assert.Single(recoveryTransactions);
        Assert.Equal(LedgerDirection.CREDIT, recoveryTransaction.Direction);
        Assert.Equal(40_000m, recoveryTransaction.Amount);
        Assert.Equal(graph.Claim.Id, recoveryTransaction.ProtectionClaimId);
        Assert.Equal((await db.ClaimRecoveries.SingleAsync()).Id, recoveryTransaction.ClaimRecoveryId);
        Assert.Equal(40_000m, (await db.RiskFundAccounts.SingleAsync()).CurrentBalance);
        Assert.Empty(await db.WalletTransactions.ToListAsync());

        var liability = await db.DriverLiabilities.SingleAsync();
        Assert.Equal(40_000m, liability.PaidAmount);
        Assert.Equal(60_000m, liability.OutstandingAmount);
        Assert.Equal(DriverLiabilityStatus.PARTIALLY_PAID, liability.Status);

        var driverView = Assert.Single(await service.GetDriverLiabilitiesAsync(
            graph.DriverId, CancellationToken.None));
        var recoveryView = Assert.Single(driverView.Recoveries);
        Assert.Equal(graph.Claim.AccidentReportId, driverView.AccidentReportId);
        Assert.Equal(ProtectionClaimStatus.RECOVERY_IN_PROGRESS, driverView.ClaimStatus);
        Assert.Equal(40_000m, recoveryView.Amount);
        Assert.EndsWith("1234", recoveryView.MaskedPaymentReference, StringComparison.Ordinal);
        Assert.DoesNotContain("DRIVER-PAYMENT", recoveryView.MaskedPaymentReference, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ManualRecovery_BeforeFundingOrWithConflictingIdempotencyKeyIsRejected()
    {
        await using var db = CreateDbContext();
        var graph = await SeedFundableClaimAsync(db, 100_000m);
        var ledger = new RiskFundLedgerService(db);
        var service = CreateAccidentService(db);
        var staffId = Guid.NewGuid();
        var request = new ClaimRecoveryRequest(
            RecoverySourceType.DRIVER,
            graph.DriverId.ToString(),
            40_000m,
            "DRIVER-PAYMENT-1234",
            ClaimEvidence("https://evidence.test/driver-recovery.pdf"),
            "recovery-conflict",
            string.Empty);

        var notFunded = await Assert.ThrowsAsync<BookingException>(() =>
            service.RecordRecoveryAsync(staffId, graph.Claim.Id, request, CancellationToken.None));
        Assert.Equal("risk_protection.recovery_not_funded", notFunded.Code);
        Assert.Empty(await db.ClaimRecoveries.ToListAsync());

        await ledger.ApplyOpeningBalanceAsync(
            staffId,
            Mutation("conflict-opening") with { Amount = 100_000m },
            CancellationToken.None);
        await service.FundClaimAsync(
            staffId, graph.Claim.Id, "conflict-funding", CancellationToken.None);
        await service.RecordRecoveryAsync(
            staffId, graph.Claim.Id, request, CancellationToken.None);

        var conflict = await Assert.ThrowsAsync<BookingException>(() =>
            service.RecordRecoveryAsync(
                staffId,
                graph.Claim.Id,
                request with { Amount = 50_000m },
                CancellationToken.None));

        Assert.Equal("risk_protection.recovery_idempotency_conflict", conflict.Code);
        Assert.Single(await db.ClaimRecoveries.ToListAsync());
        Assert.Equal(40_000m, (await db.RiskFundAccounts.SingleAsync()).CurrentBalance);
    }

    [Fact]
    public async Task ManualRecovery_CannotExceedActualOutstandingFundExposure()
    {
        await using var db = CreateDbContext();
        var graph = await SeedFundableClaimAsync(db, 100_000m);
        graph.Claim.DriverLiabilityAmount = 200_000m;
        graph.Claim.OutstandingRecoveryAmount = 200_000m;
        var liability = await db.DriverLiabilities.SingleAsync();
        liability.ConfirmedAmount = 200_000m;
        liability.OutstandingAmount = 200_000m;
        await db.SaveChangesAsync();

        var ledger = new RiskFundLedgerService(db);
        var service = CreateAccidentService(db);
        var staffId = Guid.NewGuid();
        await ledger.ApplyOpeningBalanceAsync(
            staffId,
            Mutation("exposure-opening") with { Amount = 100_000m },
            CancellationToken.None);
        await service.FundClaimAsync(
            staffId, graph.Claim.Id, "exposure-funding", CancellationToken.None);

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            service.RecordRecoveryAsync(
                staffId,
                graph.Claim.Id,
                new ClaimRecoveryRequest(
                    RecoverySourceType.DRIVER,
                    graph.DriverId.ToString(),
                    150_000m,
                    "DRIVER-OVERPAYMENT",
                    ClaimEvidence("https://evidence.test/overpayment.pdf"),
                    "exposure-recovery",
                    string.Empty),
                CancellationToken.None));

        Assert.Equal("risk_protection.recovery_exceeds_outstanding", exception.Code);
        Assert.Empty(await db.ClaimRecoveries.ToListAsync());
        Assert.Equal(0m, (await db.RiskFundAccounts.SingleAsync()).CurrentBalance);
    }

    [Fact]
    public async Task ManualRecovery_FullObligationSettlesClaimAndRestoresFundBalance()
    {
        await using var db = CreateDbContext();
        var graph = await SeedFundableClaimAsync(db, 100_000m);
        var ledger = new RiskFundLedgerService(db);
        var service = CreateAccidentService(db);
        var staffId = Guid.NewGuid();
        await ledger.ApplyOpeningBalanceAsync(
            staffId,
            Mutation("settlement-opening") with { Amount = 100_000m },
            CancellationToken.None);
        await service.FundClaimAsync(
            staffId, graph.Claim.Id, "settlement-funding", CancellationToken.None);

        var settled = await service.RecordRecoveryAsync(
            staffId,
            graph.Claim.Id,
            new ClaimRecoveryRequest(
                RecoverySourceType.DRIVER,
                graph.DriverId.ToString(),
                100_000m,
                "DRIVER-FULL-RECOVERY",
                ClaimEvidence("https://evidence.test/full-recovery.pdf"),
                "settlement-recovery",
                string.Empty),
            CancellationToken.None);

        Assert.Equal(ProtectionClaimStatus.SETTLED, settled.Status);
        Assert.Equal(0m, settled.OutstandingRecoveryAmount);
        Assert.Equal(100_000m, settled.RecoveredAmount);
        Assert.Equal(100_000m, (await db.RiskFundAccounts.SingleAsync()).CurrentBalance);
        var liability = await db.DriverLiabilities.SingleAsync();
        Assert.Equal(DriverLiabilityStatus.PAID, liability.Status);
        Assert.Equal(0m, liability.OutstandingAmount);
        Assert.Empty(await db.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task AdvanceWriteOff_ReclassifiesPermanentLossWithoutSecondFundDebit()
    {
        await using var db = CreateDbContext();
        var graph = await SeedFundableClaimAsync(db, 100_000m);
        var ledger = new RiskFundLedgerService(db);
        var service = CreateAccidentService(db);
        var staffId = Guid.NewGuid();
        await ledger.ApplyOpeningBalanceAsync(
            staffId, Mutation("write-off-opening"), CancellationToken.None);
        await service.FundClaimAsync(
            staffId, graph.Claim.Id, "write-off-funding", CancellationToken.None);
        await service.RecordRecoveryAsync(
            staffId,
            graph.Claim.Id,
            new ClaimRecoveryRequest(
                RecoverySourceType.DRIVER,
                graph.DriverId.ToString(),
                40_000m,
                "WRITE-OFF-PARTIAL-RECOVERY",
                ClaimEvidence("https://evidence.test/write-off-recovery.pdf"),
                "write-off-recovery",
                string.Empty),
            CancellationToken.None);

        var settled = await service.WriteOffAdvanceAsync(
            staffId,
            graph.Claim.Id,
            new ClaimWriteOffRequest(
                60_000m,
                "Obligation is no longer legally recoverable",
                ClaimEvidence("https://evidence.test/write-off.pdf"),
                "write-off-001",
                string.Empty),
            CancellationToken.None);

        Assert.Equal(ProtectionClaimStatus.SETTLED, settled.Status);
        Assert.Equal(60_000m, settled.WrittenOffAdvanceAmount);
        Assert.Equal(0m, settled.OutstandingRecoveryAmount);
        Assert.True(settled.IsReconciled);
        var audit = Assert.Single(await db.ClaimReconciliationRecords.ToListAsync());
        var debit = Assert.Single(await db.RiskFundTransactions.Where(x =>
            x.TransactionType == RiskFundTransactionType.CLAIM_ADVANCE).ToListAsync());
        Assert.Equal(100_000m, debit.Amount);
        Assert.Equal(40_000m, (await db.RiskFundAccounts.SingleAsync()).CurrentBalance);
        audit.Reason = "Attempted rewrite";
        var immutable = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("immutable", immutable.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CloseClaim_WhenAccountingIsUnbalanced_IsRejected()
    {
        await using var db = CreateDbContext();
        var graph = await SeedFundableClaimAsync(db, 100_000m);
        var ledger = new RiskFundLedgerService(db);
        var service = CreateAccidentService(db);
        var staffId = Guid.NewGuid();
        await ledger.ApplyOpeningBalanceAsync(
            staffId, Mutation("close-mismatch-opening"), CancellationToken.None);
        await service.FundClaimAsync(
            staffId, graph.Claim.Id, "close-mismatch-funding", CancellationToken.None);
        graph.Claim.OutstandingRecoveryAmount = 0m;
        graph.Claim.Status = ProtectionClaimStatus.SETTLED;
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            service.CloseClaimAsync(
                staffId, graph.Claim.Id, new CloseClaimRequest(string.Empty), CancellationToken.None));

        Assert.Equal("risk_protection.claim_not_reconciled", exception.Code);
        Assert.NotEqual(AccidentStatus.CLOSED, graph.Claim.AccidentReport.Status);
    }

    [Fact]
    public async Task AccidentClaim_FullRecoveryAndReconciliation_ClosesClaimAndAccident()
    {
        await using var db = CreateDbContext();
        var graph = await SeedFundableClaimAsync(db, 100_000m);
        var ledger = new RiskFundLedgerService(db);
        var service = CreateAccidentService(db);
        var staffId = Guid.NewGuid();
        await ledger.ApplyOpeningBalanceAsync(
            staffId, Mutation("close-e2e-opening"), CancellationToken.None);
        await service.FundClaimAsync(
            staffId, graph.Claim.Id, "close-e2e-funding", CancellationToken.None);
        await service.RecordRecoveryAsync(
            staffId,
            graph.Claim.Id,
            new ClaimRecoveryRequest(
                RecoverySourceType.DRIVER,
                graph.DriverId.ToString(),
                100_000m,
                "CLOSE-E2E-RECOVERY",
                ClaimEvidence("https://evidence.test/close-e2e.pdf"),
                "close-e2e-recovery",
                string.Empty),
            CancellationToken.None);

        var closed = await service.CloseClaimAsync(
            staffId, graph.Claim.Id, new CloseClaimRequest(string.Empty), CancellationToken.None);

        Assert.Equal(ProtectionClaimStatus.CLOSED, closed.Status);
        Assert.Equal(AccidentStatus.CLOSED,
            (await db.AccidentReports.SingleAsync(x => x.Id == graph.Claim.AccidentReportId)).Status);
        Assert.Empty(await db.WalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task ClaimRecovery_AuditRecordCannotBeUpdatedOrDeleted()
    {
        await using var db = CreateDbContext();
        var graph = await SeedFundableClaimAsync(db, 100_000m);
        var recovery = new ClaimRecovery
        {
            ProtectionClaimId = graph.Claim.Id,
            SourceType = RecoverySourceType.DRIVER,
            PayerReference = graph.DriverId.ToString(),
            Amount = 10_000m,
            PaymentReference = "IMMUTABLE-001",
            EvidenceUrl = "https://evidence.test/immutable.pdf",
            EvidenceStoragePublicId = "claim-evidence/immutable",
            EvidenceOriginalFileName = "immutable.pdf",
            EvidenceContentType = "application/pdf",
            EvidenceFileSizeBytes = 128,
            RecordedByUserId = Guid.NewGuid(),
            IdempotencyKey = "immutable-recovery"
        };
        db.ClaimRecoveries.Add(recovery);
        await db.SaveChangesAsync();

        recovery.Amount = 20_000m;
        var updateException = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("immutable", updateException.Message, StringComparison.OrdinalIgnoreCase);

        db.Entry(recovery).State = EntityState.Unchanged;
        db.ClaimRecoveries.Remove(recovery);
        var deleteException = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("immutable", deleteException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RiskProtectionPolicy_ReferencedByCoverage_CannotBeChanged()
    {
        await using var db = CreateDbContext();
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: false);

        graph.TripGraph.Policy.RiskReserveRate = .25m;
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        Assert.Contains("immutable", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RiskFund_Export_StreamsPastListLimitWithDeterministicFilteredOrder()
    {
        await using var db = CreateDbContext();
        db.RiskFundAccounts.Add(new RiskFundAccount { Id = 1, CurrentBalance = 2_000m });
        var start = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var index = 0; index < 1005; index++)
        {
            db.RiskFundTransactions.Add(new RiskFundTransaction
            {
                RiskFundAccountId = 1,
                TransactionType = index % 2 == 0
                    ? RiskFundTransactionType.ADJUSTMENT
                    : RiskFundTransactionType.OPENING_BALANCE,
                Direction = LedgerDirection.CREDIT,
                Amount = 1m,
                BalanceBefore = index,
                BalanceAfter = index + 1,
                PerformedByUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ExternalReference = $"EXPORT-{index}",
                EvidenceUrl = "https://evidence.test/export.pdf",
                Reason = "Export test",
                IdempotencyKey = $"export-{index}",
                CreatedAtUtc = start.AddSeconds(index / 2)
            });
        }
        await db.SaveChangesAsync();

        var ledger = new RiskFundLedgerService(db);
        await using var output = new MemoryStream();
        await ledger.ExportTransactionsAsync(
            RiskFundTransactionType.ADJUSTMENT,
            start.AddSeconds(100),
            start.AddSeconds(400),
            output,
            CancellationToken.None);

        var lines = Encoding.UTF8.GetString(output.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(302, lines.Length);
        Assert.Contains("\"EXPORT-200\"", lines[1]);
        Assert.Contains("\"EXPORT-800\"", lines[^1]);
    }

    [Fact]
    public void RiskFund_ExportEndpoint_RemainsAdminOnly()
    {
        var authorize = Assert.Single(typeof(AdminRiskFundController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());
        Assert.Equal("Admin", authorize.Roles);
        Assert.NotNull(typeof(AdminRiskFundController).GetMethod("ExportTransactions"));
    }

    [Fact]
    public async Task RiskFund_Dashboard_SeparatesAllLedgerCategoriesAndExposure()
    {
        await using var db = CreateDbContext();
        db.RiskFundAccounts.Add(new RiskFundAccount { Id = 1, CurrentBalance = 55m });
        var types = new[]
        {
            (RiskFundTransactionType.CONTRIBUTION, LedgerDirection.CREDIT, 100m),
            (RiskFundTransactionType.CLAIM_ADVANCE, LedgerDirection.DEBIT, 30m),
            (RiskFundTransactionType.CLAIM_PAYOUT, LedgerDirection.DEBIT, 20m),
            (RiskFundTransactionType.DRIVER_RECOVERY, LedgerDirection.CREDIT, 10m),
            (RiskFundTransactionType.ADJUSTMENT, LedgerDirection.CREDIT, 5m),
            (RiskFundTransactionType.ADJUSTMENT, LedgerDirection.DEBIT, 10m)
        };
        for (var index = 0; index < types.Length; index++)
        {
            var item = types[index];
            db.RiskFundTransactions.Add(new RiskFundTransaction
            {
                RiskFundAccountId = 1,
                TransactionType = item.Item1,
                Direction = item.Item2,
                Amount = item.Item3,
                BalanceBefore = 0m,
                BalanceAfter = 0m,
                Reason = "Dashboard test",
                IdempotencyKey = $"dashboard-{index}"
            });
        }
        await db.SaveChangesAsync();
        db.ProtectionClaims.Add(new ProtectionClaim
        {
            AccidentReportId = 999,
            Status = ProtectionClaimStatus.RECOVERY_IN_PROGRESS,
            RiskFundAdvanceAmount = 30m,
            RecoveredAmount = 10m,
            OutstandingRecoveryAmount = 15m,
            WrittenOffAdvanceAmount = 5m
        });
        await db.SaveChangesAsync();

        var dashboard = await new RiskFundLedgerService(db).GetDashboardAsync(CancellationToken.None);

        Assert.Equal(100m, dashboard.TotalContributions);
        Assert.Equal(30m, dashboard.ClaimAdvances);
        Assert.Equal(20m, dashboard.ClaimPayouts);
        Assert.Equal(10m, dashboard.TotalRecoveries);
        Assert.Equal(5m, dashboard.AdjustmentCredits);
        Assert.Equal(10m, dashboard.AdjustmentDebits);
        Assert.Equal(15m, dashboard.OutstandingExposure);
    }

    private static RiskFundMutationRequest Mutation(string idempotencyKey) => new(
        100_000m, LedgerDirection.CREDIT, "MVP opening balance", "BANK-001",
        "https://evidence.test/opening.pdf", idempotencyKey);

    private static TrustedClaimEvidence ClaimEvidence(string url) => new(
        url,
        $"claim-evidence/{Guid.NewGuid():N}",
        "evidence.pdf",
        "application/pdf",
        128);

    private static PreTripVehicleCheckRequest PassedCheck() => new(
        true, true, true, true, true, true, true, null, "All clear", null);

    private static PreTripVehicleCheckRequest FailedCheck() => new(
        false, true, true, true, true, true, true, VehicleFaultType.BRAKE_FAILURE,
        "Brake response failed", null);

    private static LiabilityAssessmentRequest ValidAssessment(
        DriverFaultLevel driverFaultLevel = DriverFaultLevel.ORDINARY_NEGLIGENCE) => new(
            40m,
            30m,
            10m,
            20m,
            0m,
            driverFaultLevel,
            VehicleDefectAwareness.UNKNOWN,
            new[]
            {
                new LiabilityCauseRequest(AccidentRootCause.DRIVER_ERROR, ResponsiblePartyType.DRIVER, 40m),
                new LiabilityCauseRequest(AccidentRootCause.CUSTOMER_INTERFERENCE, ResponsiblePartyType.CUSTOMER, 30m),
                new LiabilityCauseRequest(AccidentRootCause.THIRD_PARTY_ERROR, ResponsiblePartyType.THIRD_PARTY, 10m),
                new LiabilityCauseRequest(AccidentRootCause.VEHICLE_MECHANICAL_FAILURE, ResponsiblePartyType.VEHICLE, 20m)
            });

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"risk-protection-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<FundableClaimGraph> SeedFundableClaimAsync(
        ApplicationDbContext db,
        decimal fundAmount)
    {
        var graph = await SeedCoveredAccidentAsync(db, withInsurance: false);
        var claim = new ProtectionClaim
        {
            AccidentReportId = graph.Accident.Id,
            Status = ProtectionClaimStatus.APPROVED,
            TotalDamageAmount = fundAmount,
            EligibleDamageAmount = fundAmount,
            RiskFundAdvanceAmount = fundAmount,
            DriverLiabilityAmount = fundAmount,
            OutstandingRecoveryAmount = fundAmount,
            CreatedAtUtc = DateTime.UtcNow
        };
        var liability = new DriverLiability
        {
            ProtectionClaim = claim,
            DriverId = graph.TripGraph.DriverId,
            DriverAttributableEligibleDamage = fundAmount,
            FaultLevel = DriverFaultLevel.INTENTIONAL_MISCONDUCT,
            AppliedRate = 1m,
            ConfirmedAmount = fundAmount,
            OutstandingAmount = fundAmount,
            Status = DriverLiabilityStatus.CONFIRMED,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.AddRange(claim, liability);
        await db.SaveChangesAsync();
        return new FundableClaimGraph(graph.TripGraph.DriverId, claim);
    }

    private static AccidentManagementService CreateAccidentService(
        ApplicationDbContext db,
        IAccidentRealtimeService? realtime = null) =>
        new(
            db,
            new TripCommissionCalculator(),
            new RiskFundLedgerService(db),
            new MockInsuranceProvider(),
            realtime ?? new CapturingAccidentRealtimeService(),
            NullLogger<AccidentManagementService>.Instance);

    private static async Task<CoveredAccidentGraph> SeedCoveredAccidentAsync(
        ApplicationDbContext db,
        bool withInsurance)
    {
        var graph = await SeedTripAsync(db, TripStatus.IN_PROGRESS, riskEnabled: true);
        graph.Trip.StartedAt = DateTime.UtcNow.AddMinutes(-30);
        var check = new PreTripVehicleCheck
        {
            TripId = graph.Trip.Id,
            DriverId = graph.DriverId,
            BrakeResponsePassed = true,
            FrontRearLightsPassed = true,
            TurnSignalsPassed = true,
            VisibleTiresPassed = true,
            DashboardWarningPassed = true,
            WindshieldVisibilityPassed = true,
            NoMajorVisibleIssue = true,
            Result = PreTripCheckResult.PASS,
            CheckedAtUtc = graph.Trip.StartedAt.Value.AddMinutes(-5)
        };
        db.PreTripVehicleChecks.Add(check);
        await db.SaveChangesAsync();

        var coverage = new TripProtectionCoverage
        {
            TripId = graph.Trip.Id,
            PolicyVersionId = graph.Policy.Id,
            PreTripVehicleCheckId = check.Id,
            ProtectionLimit = graph.Policy.DefaultProtectionLimit,
            InsuranceProviderSnapshot = withInsurance ? "Mock insurer" : null,
            PolicyNumberSnapshot = withInsurance ? "MOCK-POLICY-001" : null,
            InsuranceCoverageSnapshot = withInsurance ? 10_000_000m : null,
            InsuranceDeductibleSnapshot = withInsurance ? 1_000_000m : null,
            ActivatedAtUtc = graph.Trip.StartedAt.Value
        };
        var accident = new AccidentReport
        {
            TripId = graph.Trip.Id,
            ReportedByUserId = graph.DriverId,
            Category = AccidentCategory.MULTIPLE,
            Status = AccidentStatus.UNDER_REVIEW,
            OccurredAtUtc = graph.Trip.StartedAt.Value.AddMinutes(10),
            Description = "Phase 6 liability test accident",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.AddRange(coverage, accident);
        await db.SaveChangesAsync();
        return new CoveredAccidentGraph(graph, accident, coverage);
    }

    private static async Task ActivateCoverageAsync(
        ApplicationDbContext db,
        TripGraph graph)
    {
        var check = new PreTripVehicleCheck
        {
            TripId = graph.Trip.Id,
            DriverId = graph.DriverId,
            BrakeResponsePassed = true,
            FrontRearLightsPassed = true,
            TurnSignalsPassed = true,
            VisibleTiresPassed = true,
            DashboardWarningPassed = true,
            WindshieldVisibilityPassed = true,
            NoMajorVisibleIssue = true,
            Result = PreTripCheckResult.PASS,
            CheckedAtUtc = graph.Trip.StartedAt!.Value.AddMinutes(-5)
        };
        db.PreTripVehicleChecks.Add(check);
        await db.SaveChangesAsync();
        db.TripProtectionCoverages.Add(new TripProtectionCoverage
        {
            TripId = graph.Trip.Id,
            PolicyVersionId = graph.Policy.Id,
            PreTripVehicleCheckId = check.Id,
            ProtectionLimit = graph.Policy.DefaultProtectionLimit,
            ActivatedAtUtc = graph.Trip.StartedAt.Value
        });
        await db.SaveChangesAsync();
    }

    private static async Task<TripGraph> SeedTripAsync(
        ApplicationDbContext db,
        TripStatus status,
        bool riskEnabled,
        bool componentAwarePricing = false)
    {
        var customerId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var customer = new AspNetUser
        {
            Id = customerId,
            UserName = $"customer-{customerId:N}@test.local",
            Email = $"customer-{customerId:N}@test.local",
            FullName = "Customer",
            CreatedAt = DateTime.UtcNow
        };
        var vehicle = new Vehicle
        {
            OwnerUserId = customerId,
            OwnerUser = customer,
            PlateNumber = $"TEST-{Guid.NewGuid():N}"[..14],
            BrandModel = "SafeRide test vehicle",
            RequiredLicenseClass = RequiredLicenseClass.A1,
            VehicleType = VehicleType.Motorbike,
            EngineType = EngineType.ICE,
            TransmissionType = TransmissionType.None,
            EngineCapacityCc = 110,
            CreatedAt = DateTime.UtcNow
        };
        var serviceType = new ServiceType { ServiceName = $"Test-{Guid.NewGuid():N}" };
        var booking = new Booking
        {
            CustomerId = customerId,
            Customer = customer,
            Vehicle = vehicle,
            ServiceType = serviceType,
            BookingStatus = status == TripStatus.COMPLETED ? BookingStatus.Completed : BookingStatus.DriverAssigned,
            PickupAddress = "Pickup",
            PickupLocation = new Point(106.7, 10.8) { SRID = 4326 },
            EstimatedFare = 100_000m,
            PricingSnapshotVersion = componentAwarePricing
                ? Booking.CurrentPricingSnapshotVersion
                : null,
            AcceptedMinimumServiceFare = componentAwarePricing ? 30_000m : null,
            SurgedFare = componentAwarePricing ? 80_000m : null,
            LongDistanceComponent = componentAwarePricing ? 20_000m : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var trip = new Trip
        {
            Booking = booking,
            DriverId = driverId,
            TripStatus = status,
            StartedAt = status == TripStatus.COMPLETED ? DateTime.UtcNow.AddHours(-1) : null,
            CompletedAt = status == TripStatus.COMPLETED ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        var policy = new RiskProtectionPolicyVersion
        {
            EffectiveFromUtc = DateTime.UtcNow.AddDays(-1),
            BasePlatformCommissionRate = .30m,
            RiskReserveRate = .10m,
            DefaultProtectionLimit = 20_000_000m,
            DriverOrdinaryNegligenceRate = .20m,
            DriverOrdinaryNegligenceCap = 2_000_000m,
            DriverGrossNegligenceRate = .50m,
            DriverGrossNegligenceCap = 5_000_000m,
            MockInsuranceCoverageLimit = 10_000_000m,
            ClaimAutoApprovalThreshold = 2_000_000m,
            RiskFundEnabled = riskEnabled,
            ChangeReason = "Integration test rollout",
            CreatedAtUtc = DateTime.UtcNow
        };
        var promotion = new Promotion
        {
            PromotionCode = $"P{Guid.NewGuid():N}"[..12],
            DiscountType = DiscountType.Fixed,
            DiscountValue = 20_000m,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(1),
            MaxUsageCount = 100,
            UsageLimitPerUser = 1,
            IsActive = true
        };
        db.AddRange(booking, trip, policy, promotion);
        await db.SaveChangesAsync();
        return new TripGraph(driverId, trip, policy, promotion);
    }

    private sealed record TripGraph(
        Guid DriverId,
        Trip Trip,
        RiskProtectionPolicyVersion Policy,
        Promotion Promotion);

    private sealed record CoveredAccidentGraph(
        TripGraph TripGraph,
        AccidentReport Accident,
        TripProtectionCoverage Coverage);

    private sealed record FundableClaimGraph(Guid DriverId, ProtectionClaim Claim);

    private sealed class CapturingAccidentRealtimeService : IAccidentRealtimeService
    {
        public AccidentCreatedEvent? LastEvent { get; private set; }

        public Task PublishAccidentCreatedAsync(
            AccidentCreatedEvent notification,
            CancellationToken cancellationToken = default)
        {
            LastEvent = notification;
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingAdminReportRealtimeService : IAdminReportRealtimeService
    {
        public ReportCreatedEvent? LastEvent { get; private set; }

        public Task PublishReportCreatedAsync(
            ReportCreatedEvent notification,
            CancellationToken cancellationToken = default)
        {
            LastEvent = notification;
            return Task.CompletedTask;
        }
    }
}
