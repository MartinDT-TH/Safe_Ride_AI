using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class TripFinancialSettlementService : ITripFinancialSettlementService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITripCommissionCalculator _calculator;
    private readonly IRiskProtectionPolicyProvider _policyProvider;
    private readonly RiskFundLedgerService _riskFundLedger;

    public TripFinancialSettlementService(
        ApplicationDbContext dbContext,
        ITripCommissionCalculator calculator,
        IRiskProtectionPolicyProvider policyProvider,
        RiskFundLedgerService riskFundLedger)
    {
        _dbContext = dbContext;
        _calculator = calculator;
        _policyProvider = policyProvider;
        _riskFundLedger = riskFundLedger;
    }

    public async Task<TripFinancialSettlement> GetOrCreateAsync(
        Trip trip, bool safetyTerminated, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.TripFinancialSettlements
            .SingleOrDefaultAsync(x => x.TripId == trip.Id, cancellationToken);
        if (existing is not null) return existing;

        var effectiveAt = trip.StartedAt ?? trip.EndedAt ?? DateTime.UtcNow;
        var coverage = await _dbContext.TripProtectionCoverages
            .Include(x => x.PolicyVersion)
            .SingleOrDefaultAsync(x => x.TripId == trip.Id, cancellationToken);
        var policy = coverage?.PolicyVersion
            ?? await _policyProvider.GetEffectivePolicyAsync(effectiveAt, cancellationToken);
        var hasCoverage = coverage is not null;
        var promotionExpense = safetyTerminated
            ? 0m
            : trip.Booking.BookingPromotions.Sum(x => x.DiscountAmount);
        var actualFare = trip.ActualFare ?? trip.Booking.EstimatedFare;
        var calculated = _calculator.Calculate(new CommissionCalculationInput(
            actualFare,
            promotionExpense,
            policy.BasePlatformCommissionRate,
            policy.RiskReserveRate,
            hasCoverage && policy.RiskFundEnabled && !safetyTerminated));
        var settlement = new TripFinancialSettlement
        {
            TripId = trip.Id,
            PolicyVersionId = policy.Id,
            CommissionBase = calculated.CommissionBase,
            PromotionExpense = calculated.PromotionExpense,
            CustomerPayableAmount = calculated.CustomerPayableAmount,
            PlatformCommissionRate = calculated.PlatformCommissionRate,
            GrossPlatformCommission = calculated.GrossPlatformCommission,
            DriverEarning = calculated.DriverEarning,
            NetPlatformCommission = calculated.NetPlatformCommission,
            RiskReserveRate = calculated.RiskReserveRate,
            RiskContribution = calculated.RiskContribution,
            NetOperatingRevenue = calculated.NetOperatingRevenue,
            IsRiskContributionEligible = hasCoverage && policy.RiskFundEnabled && !safetyTerminated,
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.TripFinancialSettlements.Add(settlement);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return settlement;
        }
        catch (DbUpdateException) when (_dbContext.Database.CurrentTransaction is null)
        {
            _dbContext.Entry(settlement).State = EntityState.Detached;
            var winner = await _dbContext.TripFinancialSettlements
                .SingleOrDefaultAsync(x => x.TripId == trip.Id, cancellationToken);
            if (winner is not null && HasSameSnapshot(winner, settlement))
                return winner;
            throw;
        }
    }

    public async Task SettleQrDriverEarningAsync(Trip trip, string? providerReference, CancellationToken cancellationToken)
    {
        var settlement = await GetOrCreateAsync(trip, IsSafetyTerminated(trip), cancellationToken);
        if (settlement.SettledAtUtc.HasValue) return;
        var existingCredit = await _dbContext.WalletTransactions.FirstOrDefaultAsync(
            x => x.TripId == trip.Id
                && x.Amount == settlement.DriverEarning
                && (x.SettlementEffect == WalletSettlementEffect.QrDriverEarning
                    || x.SettlementEffect == null
                        && x.TransactionType == WalletTransactionType.Income
                        && x.Description != null
                        && x.Description.StartsWith("SafeRide QR trip payout")),
            cancellationToken);
        if (existingCredit is not null && existingCredit.SettlementEffect is null)
            existingCredit.SettlementEffect = WalletSettlementEffect.QrDriverEarning;
        if (existingCredit is null && settlement.DriverEarning > 0)
        {
            var wallet = await GetWalletAsync(trip.DriverId, cancellationToken);
            wallet.CurrentBalance += settlement.DriverEarning;
            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                Wallet = wallet,
                TripId = trip.Id,
                TransactionType = WalletTransactionType.Income,
                SettlementEffect = WalletSettlementEffect.QrDriverEarning,
                Amount = settlement.DriverEarning,
                Description = string.IsNullOrWhiteSpace(providerReference)
                    ? "SafeRide QR trip payout"
                    : $"SafeRide QR trip payout ({providerReference})",
                CreatedAt = DateTime.UtcNow
            });
        }
        settlement.SettledAtUtc = DateTime.UtcNow;
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (_dbContext.Database.CurrentTransaction is null)
        {
            _dbContext.ChangeTracker.Clear();
            var committed = settlement.DriverEarning > 0
                ? await IsSettlementEffectCommittedAsync(
                    trip.Id,
                    WalletSettlementEffect.QrDriverEarning,
                    cancellationToken)
                : await _dbContext.TripFinancialSettlements.AsNoTracking()
                    .AnyAsync(x => x.TripId == trip.Id && x.SettledAtUtc != null, cancellationToken);
            if (!committed)
                throw;
        }
    }

    public async Task ApplyCashWalletAdjustmentAsync(Trip trip, CancellationToken cancellationToken)
    {
        var settlement = await GetOrCreateAsync(trip, IsSafetyTerminated(trip), cancellationToken);
        if (settlement.SettledAtUtc.HasValue) return;
        var amountCollectedAboveDriverEarning =
            settlement.CustomerPayableAmount - settlement.DriverEarning;
        var expectedEffect = amountCollectedAboveDriverEarning switch
        {
            > 0 => WalletSettlementEffect.CashPlatformCommission,
            < 0 => WalletSettlementEffect.CashPromotionSubsidy,
            _ => (WalletSettlementEffect?)null
        };
        if (expectedEffect.HasValue)
        {
            var expectedType = amountCollectedAboveDriverEarning > 0
                ? WalletTransactionType.Penalty
                : WalletTransactionType.Bonus;
            var expectedAmount = Math.Abs(amountCollectedAboveDriverEarning);
            var existingEffect = await _dbContext.WalletTransactions.FirstOrDefaultAsync(
                x => x.TripId == trip.Id
                    && x.Amount == expectedAmount
                    && (x.SettlementEffect == expectedEffect
                        || x.SettlementEffect == null
                            && x.TransactionType == expectedType
                            && (x.Description == "SafeRide commission for cash trip"
                                || x.Description == "SafeRide net commission for cash trip"
                                || x.Description == "SafeRide platform-funded promotion subsidy")),
                cancellationToken);
            if (existingEffect is not null)
            {
                existingEffect.SettlementEffect ??= expectedEffect;
                settlement.SettledAtUtc = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
        }

        var wallet = await GetWalletAsync(trip.DriverId, cancellationToken);
        if (amountCollectedAboveDriverEarning > 0)
        {
            if (wallet.CurrentBalance < amountCollectedAboveDriverEarning)
                throw new BookingException(
                    "payment.insufficient_driver_wallet",
                    $"Ví tài xế cần tối thiểu {amountCollectedAboveDriverEarning:N0}đ để chọn trả tiền mặt.",
                    StatusCodes.Status409Conflict);
            wallet.CurrentBalance -= amountCollectedAboveDriverEarning;
            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                Wallet = wallet, TripId = trip.Id, TransactionType = WalletTransactionType.Penalty,
                SettlementEffect = WalletSettlementEffect.CashPlatformCommission,
                Amount = amountCollectedAboveDriverEarning,
                Description = "SafeRide cash settlement amount collected above driver earning",
                CreatedAt = DateTime.UtcNow
            });
        }
        else if (amountCollectedAboveDriverEarning < 0)
        {
            var subsidy = Math.Abs(amountCollectedAboveDriverEarning);
            wallet.CurrentBalance += subsidy;
            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                Wallet = wallet, TripId = trip.Id, TransactionType = WalletTransactionType.Bonus,
                SettlementEffect = WalletSettlementEffect.CashPromotionSubsidy,
                Amount = subsidy, Description = "SafeRide platform-funded promotion subsidy", CreatedAt = DateTime.UtcNow
            });
        }
        settlement.SettledAtUtc = DateTime.UtcNow;
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (_dbContext.Database.CurrentTransaction is null)
        {
            _dbContext.ChangeTracker.Clear();
            var committed = expectedEffect.HasValue
                ? await IsSettlementEffectCommittedAsync(trip.Id, expectedEffect.Value, cancellationToken)
                : await _dbContext.TripFinancialSettlements.AsNoTracking()
                    .AnyAsync(x => x.TripId == trip.Id && x.SettledAtUtc != null, cancellationToken);
            if (!committed)
                throw;
        }
    }

    public async Task CreateContributionForCompletedTripAsync(Trip trip, CancellationToken cancellationToken)
    {
        if (trip.TripStatus != TripStatus.COMPLETED) return;
        var settlement = await GetOrCreateAsync(trip, safetyTerminated: false, cancellationToken);
        if (!settlement.IsRiskContributionEligible || settlement.RiskContribution <= 0) return;
        await _riskFundLedger.ApplyAsync(
            RiskFundTransactionType.CONTRIBUTION,
            LedgerDirection.CREDIT,
            settlement.RiskContribution,
            trip.Id,
            null,
            null,
            null,
            $"TRP-{trip.Id}",
            null,
            "Risk reserve contribution from completed trip net platform commission",
            $"trip:{trip.Id}:risk-contribution",
            cancellationToken);
    }

    private async Task<DriverWallet> GetWalletAsync(Guid driverId, CancellationToken cancellationToken)
    {
        var wallet = await _dbContext.DriverWallets.SingleOrDefaultAsync(x => x.DriverId == driverId, cancellationToken);
        if (wallet is not null) return wallet;
        wallet = new DriverWallet { DriverId = driverId, CurrentBalance = 0m };
        _dbContext.DriverWallets.Add(wallet);
        return wallet;
    }

    private static bool IsSafetyTerminated(Trip trip) =>
        trip.TripStatus == TripStatus.CANCELLED && trip.TerminationCategory == TripTerminationCategory.SAFETY;

    private async Task<bool> IsSettlementEffectCommittedAsync(
        long tripId,
        WalletSettlementEffect effect,
        CancellationToken cancellationToken) =>
        await _dbContext.TripFinancialSettlements.AsNoTracking()
            .AnyAsync(x => x.TripId == tripId && x.SettledAtUtc != null, cancellationToken)
        && await _dbContext.WalletTransactions.AsNoTracking()
            .AnyAsync(
                x => x.TripId == tripId && x.SettlementEffect == effect,
                cancellationToken);

    private static bool HasSameSnapshot(
        TripFinancialSettlement existing,
        TripFinancialSettlement candidate) =>
        existing.PolicyVersionId == candidate.PolicyVersionId
        && existing.CommissionBase == candidate.CommissionBase
        && existing.PromotionExpense == candidate.PromotionExpense
        && existing.CustomerPayableAmount == candidate.CustomerPayableAmount
        && existing.PlatformCommissionRate == candidate.PlatformCommissionRate
        && existing.GrossPlatformCommission == candidate.GrossPlatformCommission
        && existing.DriverEarning == candidate.DriverEarning
        && existing.NetPlatformCommission == candidate.NetPlatformCommission
        && existing.RiskReserveRate == candidate.RiskReserveRate
        && existing.RiskContribution == candidate.RiskContribution
        && existing.NetOperatingRevenue == candidate.NetOperatingRevenue
        && existing.IsRiskContributionEligible == candidate.IsRiskContributionEligible;
}
