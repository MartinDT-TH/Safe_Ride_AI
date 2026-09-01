using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Features.Admin.Revenue;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class AdminRevenueQueryService : IAdminRevenueQueryService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITripCommissionCalculator _commissionCalculator;

    public AdminRevenueQueryService(
        ApplicationDbContext dbContext,
        ITripCommissionCalculator commissionCalculator)
    {
        _dbContext = dbContext;
        _commissionCalculator = commissionCalculator;
    }

    public async Task<AdminRevenueQueryResult> GetAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var days = to.DayNumber - from.DayNumber + 1;
        var previousStart = start.AddDays(-days);
        var legacyRate = await GetLegacyCommissionRateAsync(cancellationToken);
        var rows = await LoadRowsAsync(start, endExclusive, legacyRate, cancellationToken);
        var previousRows = await LoadRowsAsync(previousStart, start, legacyRate, cancellationToken);

        return new AdminRevenueQueryResult(
            rows.Sum(x => x.CustomerRevenue),
            rows.Select(x => x.TripId).Distinct().Count(),
            rows.Sum(x => x.PlatformRevenue),
            previousRows.Sum(x => x.CustomerRevenue),
            previousRows.Select(x => x.TripId).Distinct().Count(),
            rows.GroupBy(x => DateOnly.FromDateTime(x.SettledAtUtc))
                .ToDictionary(group => group.Key, group => group.Sum(x => x.CustomerRevenue)),
            rows.GroupBy(x => x.ServiceName)
                .Select(group => new AdminRevenueServiceItem(
                    group.Key,
                    group.Sum(x => x.CustomerRevenue),
                    group.Select(x => x.TripId).Distinct().Count()))
                .OrderByDescending(x => x.Revenue)
                .ToArray());
    }

    public async Task<IReadOnlyList<AdminRevenueExportItem>> GetExportAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var start = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var legacyRate = await GetLegacyCommissionRateAsync(cancellationToken);
        var rows = await LoadRowsAsync(start, endExclusive, legacyRate, cancellationToken);
        return rows.OrderByDescending(x => x.SettledAtUtc)
            .Select(x => new AdminRevenueExportItem(
                x.SettledAtUtc,
                x.TripId,
                x.ServiceName,
                x.PaymentMethod?.ToString() ?? "NO_PAYMENT_REQUIRED",
                x.CustomerRevenue,
                x.PlatformRevenue))
            .ToArray();
    }

    private async Task<List<RevenueProjection>> LoadRowsAsync(
        DateTime startUtc,
        DateTime endExclusiveUtc,
        decimal legacyRate,
        CancellationToken cancellationToken)
    {
        var settlementRows = await _dbContext.TripFinancialSettlements.AsNoTracking()
            .Where(x => x.SettledAtUtc >= startUtc && x.SettledAtUtc < endExclusiveUtc)
            .Select(x => new RevenueProjection(
                x.TripId,
                x.CustomerPayableAmount,
                x.SettledAtUtc!.Value,
                x.Trip.Booking.ServiceType.ServiceName,
                x.NetOperatingRevenue,
                x.Trip.Payments
                    .Where(payment => payment.PaymentStatus == PaymentStatus.Success)
                    .OrderByDescending(payment => payment.PaidAt)
                    .Select(payment => (PaymentMethod?)payment.PaymentMethod)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        var legacyPayments = await _dbContext.Payments.AsNoTracking()
            .Where(x => x.PaymentStatus == PaymentStatus.Success
                && x.PaidAt >= startUtc && x.PaidAt < endExclusiveUtc
                && !_dbContext.TripFinancialSettlements.Any(settlement => settlement.TripId == x.TripId))
            .Select(x => new
            {
                x.TripId,
                CustomerRevenue = x.Amount,
                SettledAtUtc = x.PaidAt!.Value,
                ServiceName = x.Trip.Booking.ServiceType.ServiceName,
                ActualFare = x.Trip.ActualFare ?? x.Trip.Booking.EstimatedFare,
                PromotionExpense = x.Trip.Booking.BookingPromotions.Sum(promotion => promotion.DiscountAmount),
                x.PaymentMethod
            })
            .ToListAsync(cancellationToken);

        var legacyRows = legacyPayments.Select(x =>
        {
            var calculation = _commissionCalculator.Calculate(new CommissionCalculationInput(
                x.ActualFare,
                x.PromotionExpense,
                legacyRate,
                RiskReserveRate: 0m,
                IsRiskContributionEligible: false));
            return new RevenueProjection(
                x.TripId,
                x.CustomerRevenue,
                x.SettledAtUtc,
                x.ServiceName,
                calculation.NetOperatingRevenue,
                x.PaymentMethod);
        });

        settlementRows.AddRange(legacyRows);
        return settlementRows;
    }

    private async Task<decimal> GetLegacyCommissionRateAsync(CancellationToken cancellationToken) =>
        await _dbContext.RiskProtectionPolicyVersions.AsNoTracking()
            .OrderBy(x => x.EffectiveFromUtc)
            .Select(x => x.BasePlatformCommissionRate)
            .FirstAsync(cancellationToken);

    private sealed record RevenueProjection(
        long TripId,
        decimal CustomerRevenue,
        DateTime SettledAtUtc,
        string ServiceName,
        decimal PlatformRevenue,
        PaymentMethod? PaymentMethod);
}
