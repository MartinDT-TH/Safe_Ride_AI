using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.RiskProtection;

public sealed class TripCommissionCalculator : ITripCommissionCalculator, IClaimSettlementCalculator
{
    public CommissionCalculationResult Calculate(CommissionCalculationInput input)
    {
        if (input.ActualFare < 0 || input.PromotionExpense < 0)
            throw new ArgumentOutOfRangeException(nameof(input), "Fare and promotion expense must be non-negative.");
        EnsureRate(input.PlatformCommissionRate, nameof(input.PlatformCommissionRate));
        EnsureRate(input.RiskReserveRate, nameof(input.RiskReserveRate));

        var actualFare = RoundVnd(input.ActualFare);
        var promotionExpense = RoundVnd(input.PromotionExpense);
        var grossCommission = RoundVnd(actualFare * input.PlatformCommissionRate);
        var driverEarning = actualFare - grossCommission;
        var customerPayable = Math.Max(0m, actualFare - promotionExpense);
        var netCommission = grossCommission - promotionExpense;
        var riskContribution = input.IsRiskContributionEligible
            ? RoundVnd(Math.Max(0m, netCommission) * input.RiskReserveRate)
            : 0m;

        return new CommissionCalculationResult(
            actualFare,
            promotionExpense,
            customerPayable,
            input.PlatformCommissionRate,
            grossCommission,
            driverEarning,
            netCommission,
            input.RiskReserveRate,
            riskContribution,
            netCommission - riskContribution);
    }

    public ComponentAwareCommissionCalculationResult CalculateComponentAware(
        ComponentAwareCommissionCalculationInput input)
    {
        if (input.GrossFare < 0
            || input.FareComponent < 0
            || input.LongDistanceComponent < 0
            || input.SnapshotPromotionDiscount < 0
            || input.LongPickupCompensation < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input),
                "Fare components, promotion discount, and pickup compensation must be non-negative.");
        }

        EnsureRate(input.PlatformCommissionRate, nameof(input.PlatformCommissionRate));
        EnsureRate(input.RiskReserveRate, nameof(input.RiskReserveRate));

        var grossFare = RoundVnd(input.GrossFare);
        var fareComponent = RoundVnd(input.FareComponent);
        var longDistanceComponent = RoundVnd(input.LongDistanceComponent);
        var snapshotPromotionDiscount = RoundVnd(input.SnapshotPromotionDiscount);
        var longPickupCompensation = RoundVnd(input.LongPickupCompensation);
        if (grossFare != fareComponent + longDistanceComponent)
        {
            throw new ArgumentException(
                "Gross fare must equal fare component plus long-distance component.",
                nameof(input));
        }

        var customerPayable = Math.Max(0m, grossFare - snapshotPromotionDiscount);
        var appliedPromotionDiscount = grossFare - customerPayable;
        var grossCommission = RoundVnd(fareComponent * input.PlatformCommissionRate);
        var driverFareEarning = fareComponent - grossCommission;
        var driverPayout = driverFareEarning + longDistanceComponent + longPickupCompensation;
        var netCommission = grossCommission - appliedPromotionDiscount;
        var riskContribution = input.IsRiskContributionEligible
            ? RoundVnd(Math.Max(0m, netCommission) * input.RiskReserveRate)
            : 0m;

        return new ComponentAwareCommissionCalculationResult(
            grossFare,
            fareComponent,
            longDistanceComponent,
            snapshotPromotionDiscount,
            appliedPromotionDiscount,
            customerPayable,
            fareComponent,
            input.PlatformCommissionRate,
            grossCommission,
            driverFareEarning,
            longDistanceComponent,
            longPickupCompensation,
            driverPayout,
            appliedPromotionDiscount,
            netCommission,
            input.RiskReserveRate,
            riskContribution,
            netCommission - riskContribution - longPickupCompensation);
    }

    public DriverLiabilityCalculationResult CalculateDriverLiability(DriverLiabilityCalculationInput input)
    {
        if (input.EligibleDamage < 0 || input.DriverFaultPercentage is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(input));

        var attributable = RoundVnd(input.EligibleDamage * input.DriverFaultPercentage / 100m);
        return input.FaultLevel switch
        {
            DriverFaultLevel.NO_FAULT => new(attributable, 0m, 0m, 0m),
            DriverFaultLevel.ORDINARY_NEGLIGENCE => ApplyRule(attributable, input.OrdinaryNegligenceRate, input.OrdinaryNegligenceCap),
            DriverFaultLevel.GROSS_NEGLIGENCE => ApplyRule(attributable, input.GrossNegligenceRate, input.GrossNegligenceCap),
            DriverFaultLevel.INTENTIONAL_MISCONDUCT => new(attributable, 1m, null, attributable),
            _ => throw new ArgumentOutOfRangeException(nameof(input.FaultLevel))
        };
    }

    public ClaimLiabilityCalculationResult CalculateLiabilities(ClaimLiabilityCalculationInput input)
    {
        if (input.EligibleDamage < 0
            || input.DriverFaultPercentage is < 0 or > 100
            || input.CustomerFaultPercentage is < 0 or > 100
            || input.ThirdPartyFaultPercentage is < 0 or > 100
            || input.DriverFaultPercentage + input.CustomerFaultPercentage
                + input.ThirdPartyFaultPercentage > 100m)
            throw new ArgumentOutOfRangeException(nameof(input));

        var driver = CalculateDriverLiability(new DriverLiabilityCalculationInput(
            input.EligibleDamage,
            input.DriverFaultPercentage,
            input.DriverFaultLevel,
            input.OrdinaryNegligenceRate,
            input.OrdinaryNegligenceCap,
            input.GrossNegligenceRate,
            input.GrossNegligenceCap));
        var customer = RoundVnd(input.EligibleDamage * input.CustomerFaultPercentage / 100m);
        var thirdParty = RoundVnd(input.EligibleDamage * input.ThirdPartyFaultPercentage / 100m);
        var totalRecoverable = Math.Min(
            RoundVnd(input.EligibleDamage),
            driver.LiabilityAmount + customer + thirdParty);

        return new ClaimLiabilityCalculationResult(driver, customer, thirdParty, totalRecoverable);
    }

    private static DriverLiabilityCalculationResult ApplyRule(decimal attributable, decimal rate, decimal cap)
    {
        EnsureRate(rate, nameof(rate));
        if (cap < 0) throw new ArgumentOutOfRangeException(nameof(cap));
        return new(attributable, rate, cap, Math.Min(RoundVnd(attributable * rate), RoundVnd(cap)));
    }

    private static decimal RoundVnd(decimal amount) => decimal.Round(amount, 0, MidpointRounding.AwayFromZero);

    private static void EnsureRate(decimal rate, string name)
    {
        if (rate is < 0 or > 1) throw new ArgumentOutOfRangeException(name);
    }
}
