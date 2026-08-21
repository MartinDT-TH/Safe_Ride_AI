using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.Bookings.Services;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Application.Features.TripSharing;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.ExternalServices.PayOS;
using SafeRide.Infrastructure.Redis;
using SafeRide.Infrastructure.Services;

namespace SafeRide.IntegrationTests;

[Trait(SqlServerTestDatabase.ProviderTraitName, SqlServerTestDatabase.SqlServerProvider)]
public sealed class RiskProtectionSqlServerTests
{
    [SqlServerFact]
    public async Task ConcurrentStaffClaimUpdates_ReturnStableConcurrencyConflict()
    {
        await using var database = await SqlServerTestDatabase.CreateCurrentModelAsync("ClaimStaffConcurrency");
        var tripGraph = await SeedRiskCoveredQrTripAsync(database);
        long claimId;
        string staleRowVersion;
        await using (var seed = database.CreateDbContext())
        {
            var trip = await seed.Trips.SingleAsync(x => x.Id == tripGraph.TripId);
            var accident = new AccidentReport
            {
                TripId = trip.Id,
                ReportedByUserId = trip.DriverId,
                Category = AccidentCategory.MULTIPLE,
                Status = AccidentStatus.SETTLEMENT,
                OccurredAtUtc = trip.StartedAt!.Value.AddMinutes(5),
                Description = "Concurrent Staff claim update",
                CreatedAtUtc = DateTime.UtcNow,
                LiabilityAssessment = new AccidentLiabilityAssessment
                {
                    DriverFaultPercentage = 0m,
                    CustomerFaultPercentage = 0m,
                    ThirdPartyFaultPercentage = 0m,
                    VehicleFailurePercentage = 0m,
                    ObjectiveCausePercentage = 100m,
                    DriverFaultLevel = DriverFaultLevel.NO_FAULT,
                    VehicleDefectAwareness = VehicleDefectAwareness.UNKNOWN,
                    Status = LiabilityAssessmentStatus.CONFIRMED,
                    ConfirmedByUserId = Guid.NewGuid(),
                    ConfirmedAtUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow,
                    Causes =
                    [
                        new AccidentLiabilityCause
                        {
                            RootCause = AccidentRootCause.ROAD_CONDITION,
                            ResponsibleParty = ResponsiblePartyType.OBJECTIVE,
                            Percentage = 100m
                        }
                    ]
                },
                ProtectionClaim = new ProtectionClaim
                {
                    Status = ProtectionClaimStatus.UNDER_REVIEW,
                    CreatedAtUtc = DateTime.UtcNow
                }
            };
            seed.AccidentReports.Add(accident);
            await seed.SaveChangesAsync();
            claimId = accident.ProtectionClaim.Id;
            staleRowVersion = Convert.ToBase64String(accident.ProtectionClaim.RowVersion);
        }

        await using (var firstContext = database.CreateDbContext())
        {
            await CreateAccidentManagementService(firstContext).CalculateClaimAsync(
                Guid.NewGuid(),
                claimId,
                new CalculateClaimRequest(100_000m, 100_000m, 0m, 0m, false, staleRowVersion),
                CancellationToken.None);
        }

        await using var staleContext = database.CreateDbContext();
        var exception = await Assert.ThrowsAsync<BookingException>(() =>
            CreateAccidentManagementService(staleContext).CalculateClaimAsync(
                Guid.NewGuid(),
                claimId,
                new CalculateClaimRequest(100_000m, 100_000m, 0m, 0m, false, staleRowVersion),
                CancellationToken.None));

        Assert.Equal("risk_protection.concurrency_conflict", exception.Code);
        Assert.Equal(409, exception.StatusCode);
    }

    [SqlServerFact]
    public async Task ConcurrentAccidentEvidenceUploads_NeverExceedTwentySlots()
    {
        await using var database = await SqlServerTestDatabase.CreateCurrentModelAsync("AccidentEvidenceRace");
        var tripGraph = await SeedRiskCoveredQrTripAsync(database);
        long accidentId;
        await using (var seed = database.CreateDbContext())
        {
            var accident = new AccidentReport
            {
                TripId = tripGraph.TripId,
                ReportedByUserId = Guid.NewGuid(),
                Category = AccidentCategory.MULTIPLE,
                Status = AccidentStatus.EVIDENCE_COLLECTION,
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-1),
                Description = "Concurrent evidence cap",
                CreatedAtUtc = DateTime.UtcNow
            };
            seed.AccidentReports.Add(accident);
            await seed.SaveChangesAsync();
            accidentId = accident.Id;
            seed.AccidentEvidence.AddRange(Enumerable.Range(1, 19).Select(slot => new AccidentEvidence
            {
                AccidentReportId = accidentId,
                SequenceNumber = slot,
                UploadedByUserId = Guid.NewGuid(),
                EvidenceType = AccidentEvidenceType.PHOTO,
                FileUrl = $"https://evidence.test/{slot}.jpg",
                OriginalFileName = $"{slot}.jpg",
                ContentType = "image/jpeg",
                StoragePublicId = $"test/{accidentId}/{slot}",
                FileSizeBytes = 100,
                CreatedAtUtc = DateTime.UtcNow.AddSeconds(slot)
            }));
            await seed.SaveChangesAsync();
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Exception?> UploadAsync(int upload)
        {
            await using var context = database.CreateDbContext();
            var service = CreateAccidentManagementService(context);
            await start.Task;
            try
            {
                await service.AddEvidenceAsync(
                    Guid.NewGuid(),
                    true,
                    accidentId,
                    new AddAccidentEvidenceRequest(
                        AccidentEvidenceType.PHOTO,
                        $"https://evidence.test/concurrent-{upload}.jpg",
                        $"concurrent-{upload}.jpg",
                        "image/jpeg",
                        $"test/{accidentId}/concurrent-{upload}",
                        100,
                        DateTime.UtcNow,
                        null,
                        null,
                        null),
                    CancellationToken.None);
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        var first = UploadAsync(1);
        var second = UploadAsync(2);
        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result is null);
        var rejection = Assert.Single(results, result => result is BookingException);
        Assert.Equal("accident.evidence_limit_reached", ((BookingException)rejection!).Code);
        await using var verification = database.CreateDbContext();
        var evidence = await verification.AccidentEvidence
            .Where(x => x.AccidentReportId == accidentId)
            .ToListAsync();
        Assert.Equal(20, evidence.Count);
        Assert.Equal(20, evidence.Select(x => x.SequenceNumber).Distinct().Count());
    }

    [SqlServerFact]
    public async Task ConcurrentSafetyTermination_CreatesOneRefundObligationAndKeepsTripCancelled()
    {
        await using var database = await SqlServerTestDatabase.CreateCurrentModelAsync("SafetyTerminationRace");
        var graph = await SeedArrivedTripWithPassingCheckAsync(database);
        await using (var seed = database.CreateDbContext())
        {
            seed.Payments.Add(new Payment
            {
                TripId = graph.TripId,
                PaymentMethod = PaymentMethod.QR,
                TransactionReference = $"{graph.TripId}777",
                Amount = 100_000m,
                Currency = "VND",
                PaymentStatus = PaymentStatus.Success,
                PaidAt = graph.StartedAtUtc,
                CreatedAt = graph.StartedAtUtc
            });
            await seed.SaveChangesAsync();
        }

        await using var firstContext = database.CreateDbContext();
        await using var secondContext = database.CreateDbContext();
        var firstService = CreateTripStatusService(firstContext, new FixedDateTimeProvider(graph.StartedAtUtc));
        var secondService = CreateTripStatusService(secondContext, new FixedDateTimeProvider(graph.StartedAtUtc));
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Task.Run(async () =>
        {
            await start.Task;
            await firstService.SafetyTerminateAsync(
                graph.DriverId, false, graph.TripId, "Safety race", CancellationToken.None);
        });
        var second = Task.Run(async () =>
        {
            await start.Task;
            await secondService.SafetyTerminateAsync(
                graph.DriverId, false, graph.TripId, "Safety race", CancellationToken.None);
        });

        start.SetResult();
        await Task.WhenAll(first, second);

        await using var verification = database.CreateDbContext();
        Assert.Equal(
            TripStatus.CANCELLED,
            (await verification.Trips.SingleAsync(x => x.Id == graph.TripId)).TripStatus);
        var reconciliation = await verification.SafetyPaymentReconciliations.SingleAsync();
        Assert.Equal(SafetyPaymentReconciliationStatus.REFUND_PENDING, reconciliation.Status);
        Assert.Equal(100_000m, reconciliation.RefundObligationAmount);
        Assert.Single(await verification.ManualPaymentRefunds.ToListAsync());
        Assert.Empty(await verification.TripFinancialSettlements.ToListAsync());
        Assert.Empty(await verification.WalletTransactions.ToListAsync());
    }

    [SqlServerFact]
    public async Task ConcurrentTripStart_CreatesOneAtomicCoverageSnapshot()
    {
        await using var database = await SqlServerTestDatabase.CreateCurrentModelAsync("PreTripStartRace");
        var graph = await SeedArrivedTripWithPassingCheckAsync(database);
        await using var firstContext = database.CreateDbContext();
        await using var secondContext = database.CreateDbContext();
        var clock = new FixedDateTimeProvider(graph.StartedAtUtc);
        var firstService = CreateTripStatusService(firstContext, clock);
        var secondService = CreateTripStatusService(secondContext, clock);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = Task.Run(async () =>
        {
            await start.Task;
            await firstService.UpdateDriverTripStatusAsync(
                graph.DriverId, graph.TripId, TripStatus.IN_PROGRESS, CancellationToken.None);
        });
        var second = Task.Run(async () =>
        {
            await start.Task;
            await secondService.UpdateDriverTripStatusAsync(
                graph.DriverId, graph.TripId, TripStatus.IN_PROGRESS, CancellationToken.None);
        });

        start.SetResult();
        await Task.WhenAll(first, second);

        await using var verification = database.CreateDbContext();
        var trip = await verification.Trips.AsNoTracking()
            .SingleAsync(x => x.Id == graph.TripId);
        var coverage = await verification.TripProtectionCoverages.AsNoTracking()
            .SingleAsync(x => x.TripId == graph.TripId);
        Assert.Equal(TripStatus.IN_PROGRESS, trip.TripStatus);
        Assert.Equal(graph.StartedAtUtc, trip.StartedAt);
        Assert.Equal(graph.PassingCheckId, coverage.PreTripVehicleCheckId);
        Assert.Equal(graph.StartedAtUtc, coverage.ActivatedAtUtc);
    }

    [SqlServerFact]
    public async Task ConcurrentRiskFundDebits_OnlyOneSucceedsWithoutPartialBalance()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync("RiskFundDebitRace");
        var actor = Guid.NewGuid();
        await using (var seed = database.CreateDbContext())
        {
            await new RiskFundLedgerService(seed).ApplyOpeningBalanceAsync(
                actor,
                new RiskFundMutationRequest(100m, LedgerDirection.CREDIT, "Opening", "OPEN-1",
                    "https://example.test/opening.pdf", "opening-race"),
                CancellationToken.None);
        }

        await using var firstContext = database.CreateDbContext();
        await using var secondContext = database.CreateDbContext();
        var firstLedger = new RiskFundLedgerService(firstContext);
        var secondLedger = new RiskFundLedgerService(secondContext);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Task.Run(async () =>
        {
            await start.Task;
            return await firstLedger.ApplyAsync(
                RiskFundTransactionType.ADJUSTMENT, LedgerDirection.DEBIT, 80m,
                null, null, null, actor, "DEBIT-1", "https://example.test/debit-1.pdf",
                "Concurrent debit", "debit-race-1", CancellationToken.None);
        });
        var second = Task.Run(async () =>
        {
            await start.Task;
            return await secondLedger.ApplyAsync(
                RiskFundTransactionType.ADJUSTMENT, LedgerDirection.DEBIT, 80m,
                null, null, null, actor, "DEBIT-2", "https://example.test/debit-2.pdf",
                "Concurrent debit", "debit-race-2", CancellationToken.None);
        });

        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, value => value);
        await using var verification = database.CreateDbContext();
        Assert.Equal(20m, (await verification.RiskFundAccounts.SingleAsync()).CurrentBalance);
        Assert.Equal(2, await verification.RiskFundTransactions.CountAsync());
    }

    [SqlServerFact]
    public async Task ConcurrentClaimFunding_DebitsFullFundingOnceAndNeverMakesFundNegative()
    {
        await using var database = await SqlServerTestDatabase.CreateCurrentModelAsync("ClaimFundingRace");
        var tripGraph = await SeedRiskCoveredQrTripAsync(database);
        var staffId = Guid.NewGuid();
        long claimId;
        string rowVersion;
        await using (var seed = database.CreateDbContext())
        {
            var accident = new AccidentReport
            {
                TripId = tripGraph.TripId,
                ReportedByUserId = staffId,
                Category = AccidentCategory.MULTIPLE,
                Status = AccidentStatus.SETTLEMENT,
                OccurredAtUtc = DateTime.UtcNow.AddMinutes(-1),
                Description = "Concurrent full claim funding",
                ProtectionClaim = new ProtectionClaim
                {
                    Status = ProtectionClaimStatus.APPROVED,
                    TotalDamageAmount = 80m,
                    EligibleDamageAmount = 80m,
                    RiskFundAdvanceAmount = 40m,
                    RiskFundPermanentLossAmount = 40m,
                    OutstandingRecoveryAmount = 40m
                }
            };
            seed.AccidentReports.Add(accident);
            await seed.SaveChangesAsync();
            claimId = accident.ProtectionClaim.Id;
            rowVersion = Convert.ToBase64String(accident.ProtectionClaim.RowVersion);
            await new RiskFundLedgerService(seed).ApplyOpeningBalanceAsync(
                staffId,
                new RiskFundMutationRequest(
                    100m, LedgerDirection.CREDIT, "Opening", "CLAIM-FUNDING-RACE",
                    "https://example.test/opening.pdf", "claim-funding-opening"),
                CancellationToken.None);
        }

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<Exception?> FundAsync(string key)
        {
            await using var context = database.CreateDbContext();
            await start.Task;
            try
            {
                await CreateAccidentManagementService(context).FundClaimAsync(
                    staffId, claimId, key, rowVersion, CancellationToken.None);
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        var first = FundAsync("claim-funding-race-1");
        var second = FundAsync("claim-funding-race-2");
        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Contains(results, result => result is null);
        Assert.All(results.Where(result => result is not null), result =>
            Assert.True(result is BookingException or DbUpdateConcurrencyException));
        await using var verification = database.CreateDbContext();
        Assert.Equal(20m, (await verification.RiskFundAccounts.SingleAsync()).CurrentBalance);
        var funding = await verification.RiskFundTransactions
            .Where(x => x.ProtectionClaimId == claimId
                && (x.TransactionType == RiskFundTransactionType.CLAIM_ADVANCE
                    || x.TransactionType == RiskFundTransactionType.CLAIM_PAYOUT))
            .ToListAsync();
        Assert.Equal(2, funding.Count);
        Assert.Equal(80m, funding.Sum(x => x.Amount));
        Assert.Contains(funding, x => x.TransactionType == RiskFundTransactionType.CLAIM_ADVANCE && x.Amount == 40m);
        Assert.Contains(funding, x => x.TransactionType == RiskFundTransactionType.CLAIM_PAYOUT && x.Amount == 40m);
    }

    [SqlServerFact]
    public async Task ConcurrentOpeningBalance_SameIdempotencyPayload_ReplaysSingleTransaction()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync("RiskFundOpeningRace");
        var actor = Guid.NewGuid();
        var request = new RiskFundMutationRequest(100m, LedgerDirection.CREDIT, "Opening", "OPEN-1",
            "https://example.test/opening.pdf", "opening-idempotency-race");
        await using var firstContext = database.CreateDbContext();
        await using var secondContext = database.CreateDbContext();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Task.Run(async () =>
        {
            await start.Task;
            return await new RiskFundLedgerService(firstContext)
                .ApplyOpeningBalanceAsync(actor, request, CancellationToken.None);
        });
        var second = Task.Run(async () =>
        {
            await start.Task;
            return await new RiskFundLedgerService(secondContext)
                .ApplyOpeningBalanceAsync(actor, request, CancellationToken.None);
        });

        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Applied);
        Assert.Equal(results[0].Transaction.Id, results[1].Transaction.Id);
        await using var verification = database.CreateDbContext();
        Assert.Single(await verification.RiskFundTransactions.ToListAsync());
        Assert.Equal(100m, (await verification.RiskFundAccounts.SingleAsync()).CurrentBalance);
    }

    [SqlServerFact]
    public async Task RiskFundDatabaseGuards_BlockLedgerRewriteNegativeBalanceAndReferencedPolicyUpdate()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync("RiskFundDatabaseGuards");
        var graph = await SeedRiskCoveredQrTripAsync(database);
        await using var context = database.CreateDbContext();
        var actor = Guid.NewGuid();
        await new RiskFundLedgerService(context).ApplyOpeningBalanceAsync(
            actor,
            new RiskFundMutationRequest(100m, LedgerDirection.CREDIT, "Opening", "OPEN-1",
                "https://example.test/opening.pdf", "opening-guards"),
            CancellationToken.None);
        var ledgerId = await context.RiskFundTransactions.Select(x => x.Id).SingleAsync();
        var policyId = await context.TripProtectionCoverages
            .Where(x => x.TripId == graph.TripId)
            .Select(x => x.PolicyVersionId)
            .SingleAsync();

        await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [RiskFundTransactions] SET [Reason] = {'X'} WHERE [Id] = {ledgerId}"));
        await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM [RiskFundTransactions] WHERE [Id] = {ledgerId}"));
        await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(
            "UPDATE [RiskFundAccounts] SET [CurrentBalance] = -1 WHERE [Id] = 1"));
        await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [RiskProtectionPolicyVersions] SET [RiskReserveRate] = {0.2m} WHERE [Id] = {policyId}"));
    }

    [SqlServerFact]
    public async Task RelationalSmoke_FilteredUniqueIndexAndRowVersion_AreEnforced()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync("RiskProtectionSmoke");

        await VerifyRowVersionAsync(database);
        await VerifyFilteredUniqueIndexAsync(database);
    }

    [SqlServerFact]
    public async Task ConcurrentQrSettlement_RaceReplaysOneSnapshotAndWalletEffect()
    {
        await using var database = await SqlServerTestDatabase.CreateCurrentModelAsync("FinancialSettlementRace");
        var tripId = await SeedPayableTripAsync(database);

        await using var firstContext = database.CreateDbContext();
        await using var secondContext = database.CreateDbContext();
        var firstTrip = await LoadTripAsync(firstContext, tripId);
        var secondTrip = await LoadTripAsync(secondContext, tripId);
        var firstService = CreateFinancialSettlementService(firstContext);
        var secondService = CreateFinancialSettlementService(secondContext);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = Task.Run(async () =>
        {
            await start.Task;
            await firstService.SettleQrDriverEarningAsync(firstTrip, "QR-RACE", CancellationToken.None);
        });
        var second = Task.Run(async () =>
        {
            await start.Task;
            await secondService.SettleQrDriverEarningAsync(secondTrip, "QR-RACE", CancellationToken.None);
        });
        start.SetResult();
        await Task.WhenAll(first, second);

        await using var verification = database.CreateDbContext();
        var settlement = await verification.TripFinancialSettlements.SingleAsync(x => x.TripId == tripId);
        var effect = await verification.WalletTransactions.SingleAsync(
            x => x.TripId == tripId && x.SettlementEffect != null);
        Assert.NotNull(settlement.SettledAtUtc);
        Assert.Equal(70_000m, settlement.DriverEarning);
        Assert.Equal(WalletSettlementEffect.QrDriverEarning, effect.SettlementEffect);
        Assert.Equal(70_000m, effect.Amount);
        Assert.Equal(70_000m, (await verification.DriverWallets.SingleAsync()).CurrentBalance);
    }

    [SqlServerFact]
    public async Task ConcurrentQrWebhookCompletion_CreatesOneAtomicFinancialResult()
    {
        await using var database = await SqlServerTestDatabase.CreateCurrentModelAsync("QrWebhookCompletionRace");
        var graph = await SeedRiskCoveredQrTripAsync(database);
        await using var firstContext = database.CreateDbContext();
        await using var secondContext = database.CreateDbContext();
        var firstService = CreatePaymentService(firstContext);
        var secondService = CreatePaymentService(secondContext);
        var request = new DemoQrPaymentWebhookRequest(graph.TripId, graph.OrderCode, 80_000m);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = Task.Run(async () =>
        {
            await start.Task;
            return await firstService.ConfirmDemoQrPaymentAsync(request, CancellationToken.None);
        });
        var second = Task.Run(async () =>
        {
            await start.Task;
            return await secondService.ConfirmDemoQrPaymentAsync(request, CancellationToken.None);
        });
        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result =>
        {
            Assert.Equal(PaymentStatus.Success, result.PaymentStatus);
            Assert.Equal(TripStatus.COMPLETED, result.TripStatus);
        });

        await using var verification = database.CreateDbContext();
        var trip = await verification.Trips.SingleAsync(x => x.Id == graph.TripId);
        var promotion = await verification.Promotions.SingleAsync(x => x.Id == graph.PromotionId);
        var settlement = await verification.TripFinancialSettlements.SingleAsync(x => x.TripId == graph.TripId);
        var contribution = await verification.RiskFundTransactions.SingleAsync(
            x => x.TripId == graph.TripId && x.TransactionType == RiskFundTransactionType.CONTRIBUTION);
        var walletEffect = await verification.WalletTransactions.SingleAsync(
            x => x.TripId == graph.TripId && x.SettlementEffect != null);

        Assert.Equal(TripStatus.COMPLETED, trip.TripStatus);
        Assert.Equal(3, promotion.CurrentUsageCount);
        Assert.Equal(10_000m, settlement.NetPlatformCommission);
        Assert.Equal(1_000m, settlement.RiskContribution);
        Assert.Equal(1_000m, contribution.Amount);
        Assert.Equal(WalletSettlementEffect.QrDriverEarning, walletEffect.SettlementEffect);
        Assert.Equal(70_000m, walletEffect.Amount);
        Assert.Single(await verification.Payments.Where(x => x.TripId == graph.TripId).ToListAsync());
    }

    [SqlServerFact]
    public async Task WalletSettlementEffectUniqueIndex_BlocksSecondEffectButAllowsOrdinaryTransactions()
    {
        await using var database = await SqlServerTestDatabase.CreateCurrentModelAsync("WalletSettlementEffectUnique");
        var tripId = await SeedPayableTripAsync(database);
        await using var context = database.CreateDbContext();
        var wallet = await context.DriverWallets.SingleAsync();

        context.WalletTransactions.AddRange(
            new WalletTransaction
            {
                WalletId = wallet.Id,
                TripId = tripId,
                TransactionType = WalletTransactionType.Bonus,
                Amount = 1m,
                Description = "Valid ordinary bonus one"
            },
            new WalletTransaction
            {
                WalletId = wallet.Id,
                TripId = tripId,
                TransactionType = WalletTransactionType.Bonus,
                Amount = 2m,
                Description = "Valid ordinary bonus two"
            });
        await context.SaveChangesAsync();

        context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id,
            TripId = tripId,
            TransactionType = WalletTransactionType.Income,
            SettlementEffect = WalletSettlementEffect.QrDriverEarning,
            Amount = 70_000m
        });
        await context.SaveChangesAsync();
        context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id,
            TripId = tripId,
            TransactionType = WalletTransactionType.Bonus,
            SettlementEffect = WalletSettlementEffect.CashPromotionSubsidy,
            Amount = 70_000m
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static async Task VerifyRowVersionAsync(SqlServerTestDatabase database)
    {
        await using var firstContext = database.CreateDbContext();
        await using var secondContext = database.CreateDbContext();
        var firstAccount = await firstContext.RiskFundAccounts.SingleAsync(account => account.Id == 1);
        var staleAccount = await secondContext.RiskFundAccounts.SingleAsync(account => account.Id == 1);

        Assert.Equal(8, firstAccount.RowVersion.Length);
        var originalRowVersion = firstAccount.RowVersion.ToArray();

        firstAccount.UpdatedAtUtc = firstAccount.UpdatedAtUtc.AddSeconds(1);
        await firstContext.SaveChangesAsync();

        Assert.NotEqual(originalRowVersion, firstAccount.RowVersion);
        staleAccount.UpdatedAtUtc = staleAccount.UpdatedAtUtc.AddSeconds(2);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    private static async Task VerifyFilteredUniqueIndexAsync(SqlServerTestDatabase database)
    {
        await using var context = database.CreateDbContext();

        context.RiskFundTransactions.AddRange(
            NewTransaction(RiskFundTransactionType.ADJUSTMENT, "adjustment-1"),
            NewTransaction(RiskFundTransactionType.ADJUSTMENT, "adjustment-2"));
        await context.SaveChangesAsync();

        context.RiskFundTransactions.Add(NewTransaction(
            RiskFundTransactionType.OPENING_BALANCE,
            "opening-1"));
        await context.SaveChangesAsync();

        context.RiskFundTransactions.Add(NewTransaction(
            RiskFundTransactionType.OPENING_BALANCE,
            "opening-2"));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static RiskFundTransaction NewTransaction(
        RiskFundTransactionType transactionType,
        string idempotencyKey) => new()
        {
            RiskFundAccountId = 1,
            TransactionType = transactionType,
            Direction = LedgerDirection.CREDIT,
            Amount = 1,
            BalanceBefore = 0,
            BalanceAfter = 1,
            PerformedByUserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ExternalReference = $"TEST-{idempotencyKey}",
            EvidenceUrl = "https://example.test/evidence.pdf",
            Reason = "SQL Server relational smoke test",
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc)
        };

    private static TripFinancialSettlementService CreateFinancialSettlementService(
        SafeRide.Infrastructure.Persistence.ApplicationDbContext context) =>
        new(
            context,
            new TripCommissionCalculator(),
            new RiskProtectionPolicyProvider(context),
            new RiskFundLedgerService(context));

    private static PayOsPaymentService CreatePaymentService(
        SafeRide.Infrastructure.Persistence.ApplicationDbContext context)
    {
        var calculator = new TripCommissionCalculator();
        var policyProvider = new RiskProtectionPolicyProvider(context);
        var financialSettlement = new TripFinancialSettlementService(
            context,
            calculator,
            policyProvider,
            new RiskFundLedgerService(context));
        var paymentSettlement = new TripPaymentSettlementService(financialSettlement);
        var clock = new FixedDateTimeProvider(new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc));
        var tripStatus = CreateTripStatusService(context, clock);
        return new PayOsPaymentService(
            new HttpClient(),
            context,
            tripStatus,
            new NoOpRealtimeNotificationService(),
            paymentSettlement,
            financialSettlement,
            policyProvider,
            calculator,
            Options.Create(new PayOsOptions()));
    }

    private static TripStatusService CreateTripStatusService(
        SafeRide.Infrastructure.Persistence.ApplicationDbContext context,
        IDateTimeProvider clock)
    {
        var policyProvider = new RiskProtectionPolicyProvider(context);
        var financialSettlement = new TripFinancialSettlementService(
            context,
            new TripCommissionCalculator(),
            policyProvider,
            new RiskFundLedgerService(context));
        var paymentSettlement = new TripPaymentSettlementService(financialSettlement);
        return new TripStatusService(
            context,
            clock,
            new InMemoryRedisService(),
            new NoOpRealtimeNotificationService(),
            new NoOpTripReturnEvidenceStorage(),
            TestEvidenceValidation.Create(),
            new TripSharingServiceFake(),
            new FixedOptionsMonitor<TripTrackingOptions>(new TripTrackingOptions()),
            new NoOpMapRoutingService(),
            new TripFareFinalizationService(new FareEstimationService()),
            paymentSettlement,
            new PreTripVehicleCheckService(context, policyProvider, clock),
            financialSettlement,
            new NoOpAccountBanEvaluationService(),
            NullLogger<TripStatusService>.Instance);
    }

    private static async Task<ArrivedTripGraph> SeedArrivedTripWithPassingCheckAsync(
        SqlServerTestDatabase database)
    {
        await using var context = database.CreateDbContext();
        var startedAtUtc = new DateTime(2026, 8, 20, 6, 0, 0, DateTimeKind.Utc);
        var customerId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var customer = new AspNetUser
        {
            Id = customerId,
            UserName = $"customer-{customerId:N}@test.local",
            Email = $"customer-{customerId:N}@test.local",
            FullName = "Customer",
            IsActive = true,
            CreatedAt = startedAtUtc.AddDays(-1)
        };
        var driver = new DriverProfile
        {
            Driver = new AspNetUser
            {
                Id = driverId,
                UserName = $"driver-{driverId:N}@test.local",
                Email = $"driver-{driverId:N}@test.local",
                FullName = "Driver",
                IsActive = true,
                CreatedAt = startedAtUtc.AddDays(-1)
            },
            IdentityCardNumber = $"ID{Guid.NewGuid():N}"[..20],
            WorkStatus = DriverWorkStatus.Busy,
            CreatedAt = startedAtUtc.AddDays(-1)
        };
        var booking = new Booking
        {
            Customer = customer,
            Vehicle = new Vehicle
            {
                OwnerUser = customer,
                PlateNumber = $"T{Guid.NewGuid():N}"[..12],
                BrandModel = "Pre-trip race vehicle",
                RequiredLicenseClass = RequiredLicenseClass.A1,
                VehicleType = VehicleType.Motorbike,
                EngineType = EngineType.ICE,
                TransmissionType = TransmissionType.None,
                EngineCapacityCc = 110,
                CreatedAt = startedAtUtc.AddDays(-1)
            },
            ServiceType = new ServiceType { ServiceName = $"SQL-{Guid.NewGuid():N}" },
            BookingStatus = BookingStatus.DriverAssigned,
            PickupAddress = "Pickup",
            PickupLocation = new Point(106.7, 10.8) { SRID = 4326 },
            EstimatedFare = 100_000m,
            CreatedAt = startedAtUtc.AddHours(-1),
            UpdatedAt = startedAtUtc.AddHours(-1)
        };
        var trip = new Trip
        {
            Booking = booking,
            Driver = driver,
            TripStatus = TripStatus.ARRIVED,
            ArrivedAt = startedAtUtc.AddMinutes(-10),
            CreatedAt = startedAtUtc.AddHours(-1)
        };
        var policy = new RiskProtectionPolicyVersion
        {
            EffectiveFromUtc = startedAtUtc.AddDays(-2),
            BasePlatformCommissionRate = .30m,
            RiskReserveRate = .10m,
            DefaultProtectionLimit = 20_000_000m,
            DriverOrdinaryNegligenceRate = 0m,
            DriverOrdinaryNegligenceCap = 0m,
            DriverGrossNegligenceRate = 0m,
            DriverGrossNegligenceCap = 0m,
            MockInsuranceCoverageLimit = 0m,
            ClaimAutoApprovalThreshold = 0m,
            RiskFundEnabled = true,
            ChangeReason = "Pre-trip concurrency rollout",
            CreatedAtUtc = startedAtUtc.AddDays(-2)
        };
        context.AddRange(booking, trip, policy);
        await context.SaveChangesAsync();
        var check = new PreTripVehicleCheck
        {
            TripId = trip.Id,
            DriverId = driverId,
            BrakeResponsePassed = true,
            FrontRearLightsPassed = true,
            TurnSignalsPassed = true,
            VisibleTiresPassed = true,
            DashboardWarningPassed = true,
            WindshieldVisibilityPassed = true,
            NoMajorVisibleIssue = true,
            Result = PreTripCheckResult.PASS,
            CheckedAtUtc = startedAtUtc.AddMinutes(-5)
        };
        context.PreTripVehicleChecks.Add(check);
        await context.SaveChangesAsync();
        return new ArrivedTripGraph(trip.Id, driverId, check.Id, startedAtUtc);
    }

    private static AccidentManagementService CreateAccidentManagementService(
        SafeRide.Infrastructure.Persistence.ApplicationDbContext context) =>
        new(
            context,
            new TripCommissionCalculator(),
            new RiskFundLedgerService(context),
            new MockInsuranceProvider(),
            new NoOpAccidentRealtimeService(),
            NullLogger<AccidentManagementService>.Instance);

    private static Task<Trip> LoadTripAsync(
        SafeRide.Infrastructure.Persistence.ApplicationDbContext context,
        long tripId) =>
        context.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .SingleAsync(x => x.Id == tripId);

    private static async Task<long> SeedPayableTripAsync(SqlServerTestDatabase database)
    {
        await using var context = database.CreateDbContext();
        var now = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc);
        var customerId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var customer = new AspNetUser
        {
            Id = customerId,
            UserName = $"customer-{customerId:N}@test.local",
            Email = $"customer-{customerId:N}@test.local",
            FullName = "Customer",
            IsActive = true,
            CreatedAt = now
        };
        var driverUser = new AspNetUser
        {
            Id = driverId,
            UserName = $"driver-{driverId:N}@test.local",
            Email = $"driver-{driverId:N}@test.local",
            FullName = "Driver",
            IsActive = true,
            CreatedAt = now
        };
        var driver = new DriverProfile
        {
            DriverId = driverId,
            Driver = driverUser,
            IdentityCardNumber = $"ID{Guid.NewGuid():N}"[..20],
            WorkStatus = DriverWorkStatus.Busy,
            CreatedAt = now
        };
        var vehicle = new Vehicle
        {
            OwnerUser = customer,
            PlateNumber = $"T{Guid.NewGuid():N}"[..12],
            BrandModel = "SafeRide SQL test vehicle",
            RequiredLicenseClass = RequiredLicenseClass.A1,
            VehicleType = VehicleType.Motorbike,
            EngineType = EngineType.ICE,
            TransmissionType = TransmissionType.None,
            EngineCapacityCc = 110,
            CreatedAt = now
        };
        var booking = new Booking
        {
            Customer = customer,
            Vehicle = vehicle,
            ServiceType = new ServiceType { ServiceName = $"SQL-{Guid.NewGuid():N}" },
            BookingStatus = BookingStatus.DriverAssigned,
            PickupAddress = "Pickup",
            PickupLocation = new Point(106.7, 10.8) { SRID = 4326 },
            EstimatedFare = 100_000m,
            CreatedAt = now,
            UpdatedAt = now
        };
        var trip = new Trip
        {
            Booking = booking,
            Driver = driver,
            TripStatus = TripStatus.WAITING_PAYMENT,
            StartedAt = now.AddHours(-1),
            EndedAt = now,
            ActualFare = 100_000m,
            FinalFare = 100_000m,
            CreatedAt = now.AddHours(-1)
        };
        context.AddRange(booking, trip, new DriverWallet
        {
            Driver = driver,
            CurrentBalance = 0m
        });
        await context.SaveChangesAsync();
        return trip.Id;
    }

    private static async Task<RiskCoveredQrGraph> SeedRiskCoveredQrTripAsync(
        SqlServerTestDatabase database)
    {
        await using var context = database.CreateDbContext();
        var now = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc);
        var customerId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var customer = new AspNetUser
        {
            Id = customerId,
            UserName = $"customer-{customerId:N}@test.local",
            Email = $"customer-{customerId:N}@test.local",
            FullName = "Customer",
            IsActive = true,
            CreatedAt = now
        };
        var driverUser = new AspNetUser
        {
            Id = driverId,
            UserName = $"driver-{driverId:N}@test.local",
            Email = $"driver-{driverId:N}@test.local",
            FullName = "Driver",
            IsActive = true,
            CreatedAt = now
        };
        var driver = new DriverProfile
        {
            Driver = driverUser,
            IdentityCardNumber = $"ID{Guid.NewGuid():N}"[..20],
            WorkStatus = DriverWorkStatus.Busy,
            CreatedAt = now
        };
        var promotion = new Promotion
        {
            PromotionCode = $"P{Guid.NewGuid():N}"[..12],
            DiscountType = DiscountType.Fixed,
            DiscountValue = 20_000m,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(1),
            MaxUsageCount = 100,
            CurrentUsageCount = 2,
            MinimumOrderValue = 0m,
            MaximumDiscountValue = 20_000m,
            UsageLimitPerUser = 1,
            IsActive = true
        };
        var booking = new Booking
        {
            Customer = customer,
            Vehicle = new Vehicle
            {
                OwnerUser = customer,
                PlateNumber = $"T{Guid.NewGuid():N}"[..12],
                BrandModel = "SafeRide SQL race vehicle",
                RequiredLicenseClass = RequiredLicenseClass.A1,
                VehicleType = VehicleType.Motorbike,
                EngineType = EngineType.ICE,
                TransmissionType = TransmissionType.None,
                EngineCapacityCc = 110,
                CreatedAt = now
            },
            ServiceType = new ServiceType { ServiceName = $"SQL-{Guid.NewGuid():N}" },
            BookingStatus = BookingStatus.DriverAssigned,
            PickupAddress = "Pickup",
            PickupLocation = new Point(106.7, 10.8) { SRID = 4326 },
            EstimatedFare = 100_000m,
            CreatedAt = now,
            UpdatedAt = now
        };
        booking.BookingPromotions.Add(new BookingPromotion
        {
            Booking = booking,
            Promotion = promotion,
            DiscountAmount = 20_000m,
            CreatedAt = now
        });
        var trip = new Trip
        {
            Booking = booking,
            Driver = driver,
            TripStatus = TripStatus.WAITING_PAYMENT,
            StartedAt = now.AddHours(-1),
            EndedAt = now,
            ActualFare = 100_000m,
            FinalFare = 80_000m,
            CreatedAt = now.AddHours(-1)
        };
        var policy = new RiskProtectionPolicyVersion
        {
            EffectiveFromUtc = now.AddDays(-2),
            BasePlatformCommissionRate = .30m,
            RiskReserveRate = .10m,
            DefaultProtectionLimit = 20_000_000m,
            DriverOrdinaryNegligenceRate = 0m,
            DriverOrdinaryNegligenceCap = 0m,
            DriverGrossNegligenceRate = 0m,
            DriverGrossNegligenceCap = 0m,
            MockInsuranceCoverageLimit = 0m,
            ClaimAutoApprovalThreshold = 0m,
            RiskFundEnabled = true,
            ChangeReason = "SQL concurrency policy",
            CreatedAtUtc = now.AddDays(-2)
        };
        context.AddRange(booking, trip, policy, new DriverWallet
        {
            Driver = driver,
            CurrentBalance = 0m
        });
        await context.SaveChangesAsync();

        var check = new PreTripVehicleCheck
        {
            TripId = trip.Id,
            DriverId = driverId,
            BrakeResponsePassed = true,
            FrontRearLightsPassed = true,
            TurnSignalsPassed = true,
            VisibleTiresPassed = true,
            DashboardWarningPassed = true,
            WindshieldVisibilityPassed = true,
            NoMajorVisibleIssue = true,
            Result = PreTripCheckResult.PASS,
            CheckedAtUtc = now.AddHours(-2)
        };
        context.PreTripVehicleChecks.Add(check);
        await context.SaveChangesAsync();
        context.TripProtectionCoverages.Add(new TripProtectionCoverage
        {
            TripId = trip.Id,
            PolicyVersionId = policy.Id,
            PreTripVehicleCheckId = check.Id,
            ProtectionLimit = policy.DefaultProtectionLimit,
            ActivatedAtUtc = trip.StartedAt!.Value
        });
        const string orderCode = "202608200001";
        context.Payments.Add(new Payment
        {
            TripId = trip.Id,
            PaymentMethod = PaymentMethod.QR,
            TransactionReference = orderCode,
            Amount = 80_000m,
            Currency = "VND",
            PaymentStatus = PaymentStatus.Pending,
            CreatedAt = now
        });
        await context.SaveChangesAsync();
        return new RiskCoveredQrGraph(trip.Id, promotion.Id, orderCode);
    }

    private sealed record RiskCoveredQrGraph(long TripId, long PromotionId, string OrderCode);

    private sealed record ArrivedTripGraph(
        long TripId,
        Guid DriverId,
        long PassingCheckId,
        DateTime StartedAtUtc);

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FixedOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = currentValue;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class NoOpAccountBanEvaluationService : IAccountBanEvaluationService
    {
        public Task EvaluateRatingAsync(long ratingId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class NoOpAccidentRealtimeService : IAccidentRealtimeService
    {
        public Task PublishAccidentCreatedAsync(
            AccidentCreatedEvent notification,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NoOpTripReturnEvidenceStorage : ITripReturnEvidenceStorage
    {
        public Task<StoredReturnEvidenceFile> SaveAsync(
            long tripId,
            int displayOrder,
            string originalFileName,
            string contentType,
            Stream content,
            CancellationToken cancellationToken) =>
            Task.FromResult(new StoredReturnEvidenceFile(
                "https://example.test/evidence.jpg",
                null,
                originalFileName,
                contentType,
                content.Length));
    }

    private sealed class NoOpMapRoutingService : IMapRoutingService
    {
        public Task<RouteEstimateResult> GetRouteEstimateAsync(
            RouteEstimateRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new RouteEstimateResult
            {
                Provider = MapProvider.Auto,
                DistanceMeters = 0,
                DurationSeconds = 0
            });
    }

    private sealed class NoOpRealtimeNotificationService : IRealtimeNotificationService
    {
        public Task PublishBookingStatusChangedAsync(BookingStatusChangedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishBookingSearchingStartedAsync(BookingSearchingStartedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishTripCreatedAsync(TripCreatedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishBookingDriverAssignedAsync(BookingDriverAssignedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishTripStatusChangedAsync(TripStatusChangedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishTripPaymentPendingAsync(TripPaymentPendingEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishTripPaymentSucceededAsync(TripPaymentSucceededEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishSOSTriggeredAsync(SOSTriggeredEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverLocationUpdatedAsync(DriverLocationUpdatedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverOfferCreatedAsync(DriverOfferCreatedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverOfferReceivedAsync(DriverOfferReceivedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverOfferRejectedAsync(DriverOfferRejectedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverOfferAcceptedAsync(DriverOfferAcceptedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverOfferExpiredAsync(DriverOfferExpiredEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverOfferCancelledAsync(DriverOfferCancelledEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverMatchedAsync(DriverMatchedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishCustomerConfirmedDriverOfferAsync(CustomerConfirmedDriverOfferEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishBookingSearchRadiusExpandedAsync(BookingSearchRadiusExpandedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishBookingExpiredAsync(BookingExpiredEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
