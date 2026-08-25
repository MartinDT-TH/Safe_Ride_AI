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
        if (!safetyTerminated
            && trip.Booking.PricingSnapshotVersion == Booking.CurrentPricingSnapshotVersion
            && !trip.ActualFare.HasValue)
        {
            throw new BookingException(
                "settlement.fare_not_finalized",
                "Giá chuyến đi chưa được chốt nên chưa thể tạo quyết toán.",
                StatusCodes.Status409Conflict);
        }
        if (!safetyTerminated
            && trip.Booking.PricingSnapshotVersion > Booking.CurrentPricingSnapshotVersion)
        {
            throw new BookingException(
                "settlement.pricing_snapshot_version_unsupported",
                "Phiên bản dữ liệu giá của chuyến đi chưa được settlement hiện tại hỗ trợ.",
                StatusCodes.Status409Conflict);
        }
        var actualFare = trip.ActualFare ?? trip.Booking.EstimatedFare;
        var isRiskContributionEligible = hasCoverage
            && policy.RiskFundEnabled
            && !safetyTerminated;
        var settlement = !safetyTerminated
            && trip.Booking.PricingSnapshotVersion == Booking.CurrentPricingSnapshotVersion
                ? await CreateComponentAwareSettlementAsync(
                    trip,
                    actualFare,
                    promotionExpense,
                    policy,
                    isRiskContributionEligible,
                    cancellationToken)
                : CreateLegacySettlement(
                    trip,
                    actualFare,
                    promotionExpense,
                    policy,
                    isRiskContributionEligible);
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
        var driverPayout = GetDriverPayout(settlement);
        var existingCredit = await _dbContext.WalletTransactions.FirstOrDefaultAsync(
            x => x.TripId == trip.Id
                && x.Amount == driverPayout
                && (x.SettlementEffect == WalletSettlementEffect.QrDriverEarning
                    || x.SettlementEffect == null
                        && x.TransactionType == WalletTransactionType.Income
                        && x.Description != null
                        && x.Description.StartsWith("SafeRide QR trip payout")),
            cancellationToken);
        if (existingCredit is not null && existingCredit.SettlementEffect is null)
            existingCredit.SettlementEffect = WalletSettlementEffect.QrDriverEarning;
        if (existingCredit is null && driverPayout > 0)
        {
            var wallet = await GetWalletAsync(trip.DriverId, cancellationToken);
            wallet.CurrentBalance += driverPayout;
            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                Wallet = wallet,
                TripId = trip.Id,
                TransactionType = WalletTransactionType.Income,
                SettlementEffect = WalletSettlementEffect.QrDriverEarning,
                Amount = driverPayout,
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
            var committed = driverPayout > 0
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
        var amountCollectedAboveDriverPayout =
            settlement.CustomerPayableAmount - GetDriverPayout(settlement);
        var expectedEffect = amountCollectedAboveDriverPayout switch
        {
            > 0 => WalletSettlementEffect.CashPlatformCommission,
            < 0 => WalletSettlementEffect.CashPromotionSubsidy,
            _ => (WalletSettlementEffect?)null
        };
        if (expectedEffect.HasValue)
        {
            var expectedType = amountCollectedAboveDriverPayout > 0
                ? WalletTransactionType.Penalty
                : WalletTransactionType.Bonus;
            var expectedAmount = Math.Abs(amountCollectedAboveDriverPayout);
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
        if (amountCollectedAboveDriverPayout > 0)
        {
            if (wallet.CurrentBalance < amountCollectedAboveDriverPayout)
                throw new BookingException(
                    "payment.insufficient_driver_wallet",
                    $"Ví tài xế cần tối thiểu {amountCollectedAboveDriverPayout:N0}đ để chọn trả tiền mặt.",
                    StatusCodes.Status409Conflict);
            wallet.CurrentBalance -= amountCollectedAboveDriverPayout;
            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                Wallet = wallet, TripId = trip.Id, TransactionType = WalletTransactionType.Penalty,
                SettlementEffect = WalletSettlementEffect.CashPlatformCommission,
                Amount = amountCollectedAboveDriverPayout,
                Description = "SafeRide cash settlement amount collected above driver earning",
                CreatedAt = DateTime.UtcNow
            });
        }
        else if (amountCollectedAboveDriverPayout < 0)
        {
            var subsidy = Math.Abs(amountCollectedAboveDriverPayout);
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

    private TripFinancialSettlement CreateLegacySettlement(
        Trip trip,
        decimal actualFare,
        decimal promotionExpense,
        RiskProtectionPolicyVersion policy,
        bool isRiskContributionEligible)
    {
        var calculated = _calculator.Calculate(new CommissionCalculationInput(
            actualFare,
            promotionExpense,
            policy.BasePlatformCommissionRate,
            policy.RiskReserveRate,
            isRiskContributionEligible));
        return new TripFinancialSettlement
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
            IsRiskContributionEligible = isRiskContributionEligible,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private async Task<TripFinancialSettlement> CreateComponentAwareSettlementAsync(
        Trip trip,
        decimal actualFare,
        decimal snapshotPromotionDiscount,
        RiskProtectionPolicyVersion policy,
        bool isRiskContributionEligible,
        CancellationToken cancellationToken)
    {
        var (fareComponent, longDistanceComponent) = ResolveFinalizedComponents(trip, actualFare);
        // StartedAt is the durable earning boundary: the pickup component is earned
        // when the trip first reaches IN_PROGRESS and is not revoked by a later,
        // otherwise-valid early-stop reason.
        var longPickupCompensation = trip.StartedAt.HasValue
            ? await GetAcceptedLongPickupCompensationAsync(trip, cancellationToken)
            : 0m;
        var calculated = _calculator.CalculateComponentAware(
            new ComponentAwareCommissionCalculationInput(
                actualFare,
                fareComponent,
                longDistanceComponent,
                snapshotPromotionDiscount,
                longPickupCompensation,
                policy.BasePlatformCommissionRate,
                policy.RiskReserveRate,
                isRiskContributionEligible));

        return new TripFinancialSettlement
        {
            TripId = trip.Id,
            PolicyVersionId = policy.Id,
            ComponentBreakdownVersion = TripFinancialSettlement.CurrentComponentBreakdownVersion,
            GrossFare = calculated.GrossFare,
            FareComponent = calculated.FareComponent,
            LongDistanceComponent = calculated.LongDistanceComponent,
            SnapshotPromotionDiscount = calculated.SnapshotPromotionDiscount,
            AppliedPromotionDiscount = calculated.AppliedPromotionDiscount,
            CommissionBase = calculated.CommissionBase,
            PromotionExpense = calculated.PromotionExpense,
            CustomerPayableAmount = calculated.CustomerPayableAmount,
            PlatformCommissionRate = calculated.PlatformCommissionRate,
            GrossPlatformCommission = calculated.GrossPlatformCommission,
            DriverFareEarning = calculated.DriverFareEarning,
            LongDistanceEarning = calculated.LongDistanceEarning,
            LongPickupCompensation = calculated.LongPickupCompensation,
            DriverPayout = calculated.DriverPayout,
            // Compatibility: existing reports read DriverEarning; V1 stores total payout here.
            DriverEarning = calculated.DriverPayout,
            NetPlatformCommission = calculated.NetPlatformCommission,
            RiskReserveRate = calculated.RiskReserveRate,
            RiskContribution = calculated.RiskContribution,
            NetOperatingRevenue = calculated.NetOperatingRevenue,
            IsRiskContributionEligible = isRiskContributionEligible,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static (decimal FareComponent, decimal LongDistanceComponent) ResolveFinalizedComponents(
        Trip trip,
        decimal actualFare)
    {
        var grossFare = RoundVnd(actualFare);
        if (trip.EndReason == TripEndReason.CUSTOMER_REQUESTED_STOP)
        {
            if (!trip.PlannedRouteProgress.HasValue)
            {
                throw ComponentBreakdownUnavailable(
                    "Chuyến kết thúc sớm chưa có tiến độ lộ trình đã khóa.");
            }

            var allocation =
                TripFareFinalizationService.CalculateCustomerRequestedStopComponentAllocation(
                    trip.Booking,
                    trip.PlannedRouteProgress.Value);
            if (grossFare != allocation.GrossFare)
            {
                throw ComponentBreakdownUnavailable(
                    "Tổng giá đã chốt không khớp với phân bổ theo tiến độ lộ trình đã khóa.");
            }

            return (allocation.FareComponent, allocation.LongDistanceComponent);
        }

        if (!trip.Booking.SurgedFare.HasValue || !trip.Booking.LongDistanceComponent.HasValue)
        {
            throw ComponentBreakdownUnavailable(
                "Snapshot giá V1 không có đủ hai thành phần giá.");
        }

        var acceptedFareComponent = RoundVnd(trip.Booking.SurgedFare.Value);
        var acceptedLongDistanceComponent = RoundVnd(trip.Booking.LongDistanceComponent.Value);
        var acceptedGrossFare = RoundVnd(trip.Booking.EstimatedFare);
        if (acceptedGrossFare != acceptedFareComponent + acceptedLongDistanceComponent)
        {
            throw ComponentBreakdownUnavailable(
                "Các thành phần giá V1 không khớp với tổng giá đã khóa.");
        }

        if (grossFare == acceptedGrossFare)
            return (acceptedFareComponent, acceptedLongDistanceComponent);

        if (grossFare == 0m
            && trip.EndReason is TripEndReason.DRIVER_UNABLE_TO_CONTINUE
                or TripEndReason.STARTED_BY_MISTAKE)
        {
            return (0m, 0m);
        }

        throw ComponentBreakdownUnavailable(
            "Phase 2 chưa lưu phân bổ giá chốt giữa phần cước và phần đường dài cho tổng giá đã điều chỉnh này.");
    }

    private async Task<decimal> GetAcceptedLongPickupCompensationAsync(
        Trip trip,
        CancellationToken cancellationToken)
    {
        var compensation = await _dbContext.BookingDriverOffers
            .AsNoTracking()
            .Where(x => x.BookingId == trip.BookingId
                && x.DriverId == trip.DriverId
                && x.OfferStatus == DriverOfferStatus.CustomerConfirmed)
            .OrderByDescending(x => x.ConfirmedAt ?? x.OfferedAt)
            .Select(x => x.LongPickupCompensation)
            .FirstOrDefaultAsync(cancellationToken);
        return RoundVnd(compensation ?? 0m);
    }

    private static BookingException ComponentBreakdownUnavailable(string detail) =>
        new(
            "settlement.component_breakdown_unavailable",
            $"Không thể quyết toán theo thành phần giá đã khóa. {detail}",
            StatusCodes.Status409Conflict);

    private static decimal GetDriverPayout(TripFinancialSettlement settlement) =>
        settlement.DriverPayout ?? settlement.DriverEarning;

    private static decimal RoundVnd(decimal amount) =>
        decimal.Round(amount, 0, MidpointRounding.AwayFromZero);

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
        && existing.IsRiskContributionEligible == candidate.IsRiskContributionEligible
        && existing.ComponentBreakdownVersion == candidate.ComponentBreakdownVersion
        && existing.GrossFare == candidate.GrossFare
        && existing.FareComponent == candidate.FareComponent
        && existing.LongDistanceComponent == candidate.LongDistanceComponent
        && existing.SnapshotPromotionDiscount == candidate.SnapshotPromotionDiscount
        && existing.AppliedPromotionDiscount == candidate.AppliedPromotionDiscount
        && existing.DriverFareEarning == candidate.DriverFareEarning
        && existing.LongDistanceEarning == candidate.LongDistanceEarning
        && existing.LongPickupCompensation == candidate.LongPickupCompensation
        && existing.DriverPayout == candidate.DriverPayout;
}
