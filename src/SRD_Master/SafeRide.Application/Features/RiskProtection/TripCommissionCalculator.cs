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
            || input.InsurancePaidAmount < 0
            || input.CustomerInsuranceAppliedAmount < 0
            || input.DriverFaultPercentage is < 0 or > 100
            || input.CustomerFaultPercentage is < 0 or > 100
            || input.ThirdPartyFaultPercentage is < 0 or > 100
            || input.VehicleFailurePercentage is < 0 or > 100
            || input.ObjectiveCausePercentage is < 0 or > 100
            || input.DriverFaultPercentage + input.CustomerFaultPercentage
                + input.ThirdPartyFaultPercentage + input.VehicleFailurePercentage
                + input.ObjectiveCausePercentage > 100m)
            throw new ArgumentOutOfRangeException(nameof(input));

        var grossAllocations = AllocateResidual(
            RoundVnd(input.EligibleDamage),
            input.DriverFaultPercentage,
            input.CustomerFaultPercentage,
            input.ThirdPartyFaultPercentage,
            input.VehicleFailurePercentage,
            input.ObjectiveCausePercentage);
        var driverGross = grossAllocations[0];
        var customerGross = grossAllocations[1];
        var thirdPartyGross = grossAllocations[2];
        var vehicleObjectiveGross = grossAllocations[3] + grossAllocations[4];
        var customerInsurance = RoundVnd(input.CustomerInsuranceAppliedAmount);
        if (customerInsurance > RoundVnd(input.EligibleDamage))
            throw new ArgumentOutOfRangeException(nameof(input.CustomerInsuranceAppliedAmount));

        var customerInsuranceBenefit = Math.Min(customerInsurance, customerGross);
        var customerInsuranceExcess = customerInsurance - customerInsuranceBenefit;
        var driverCustomerInsuranceBenefit = Math.Min(customerInsuranceExcess, driverGross);
        var unallocatedCategoryReduction = customerInsuranceExcess - driverCustomerInsuranceBenefit;
        var customerAfterOwnInsurance = customerGross - customerInsuranceBenefit;
        var driverAfterCustomerInsurance = driverGross - driverCustomerInsuranceBenefit;
        var remainingAfterCustomerInsurance = RoundVnd(input.EligibleDamage - customerInsurance);
        var participantExposure = customerAfterOwnInsurance + driverAfterCustomerInsurance;
        var systemInsurance = RoundVnd(input.InsurancePaidAmount);
        if (systemInsurance > participantExposure)
            throw new ArgumentOutOfRangeException(nameof(input.InsurancePaidAmount));
        var systemBenefits = AllocateByWeight(
            systemInsurance,
            customerAfterOwnInsurance,
            driverAfterCustomerInsurance);
        var customerSystemBenefit = systemBenefits[0];
        var driverSystemBenefit = systemBenefits[1];
        var customerFinal = customerAfterOwnInsurance - customerSystemBenefit;
        var driverFinal = driverAfterCustomerInsurance - driverSystemBenefit;
        var residual = RoundVnd(input.EligibleDamage - customerInsurance - systemInsurance);
        var driver = CalculateDriverLiability(new DriverLiabilityCalculationInput(
            driverFinal,
            100m,
            input.DriverFaultLevel,
            input.OrdinaryNegligenceRate,
            input.OrdinaryNegligenceCap,
            input.GrossNegligenceRate,
            input.GrossNegligenceCap));

        driver = driver with
        {
            DriverAttributableEligibleDamage = driverFinal,
            LiabilityAmount = RecalculateDriverLiability(driverFinal, input)
        };
        var totalRecoverable = Math.Min(
            residual,
            driver.LiabilityAmount + customerFinal + thirdPartyGross);

        return new ClaimLiabilityCalculationResult(driver, customerFinal, thirdPartyGross, totalRecoverable)
        {
            ResidualUninsuredDamage = residual,
            CustomerGrossExposure = customerGross,
            DriverGrossExposure = driverGross,
            ThirdPartyGrossExposure = thirdPartyGross,
            VehicleObjectiveGrossExposure = vehicleObjectiveGross,
            CustomerInsuranceAppliedAmount = customerInsurance,
            CustomerInsuranceBenefitToCustomer = customerInsuranceBenefit,
            CustomerInsuranceExcessAppliedToOtherLoss = customerInsuranceExcess,
            CustomerInsuranceBenefitToDriver = driverCustomerInsuranceBenefit,
            CustomerInsuranceUnallocatedCategoryReduction = unallocatedCategoryReduction,
            CustomerExposureAfterOwnInsurance = customerAfterOwnInsurance,
            DriverExposureBeforeSystemInsurance = driverAfterCustomerInsurance,
            RemainingLossAfterCustomerInsurance = remainingAfterCustomerInsurance,
            ParticipantExposureBeforeSystemInsurance = participantExposure,
            SystemInsuranceApprovedAmount = systemInsurance,
            CustomerSystemInsuranceBenefit = customerSystemBenefit,
            DriverSystemInsuranceBenefit = driverSystemBenefit,
            CustomerFinalExposure = customerFinal,
            DriverRemainingExposureBeforeRateCap = driverFinal,
            CustomerAttributableResidualDamage = customerFinal,
            ThirdPartyAttributableResidualDamage = thirdPartyGross,
            VehicleObjectiveResidualAmount = vehicleObjectiveGross
        };
    }

    private static DriverLiabilityCalculationResult ApplyRule(decimal attributable, decimal rate, decimal cap)
    {
        EnsureRate(rate, nameof(rate));
        if (cap < 0) throw new ArgumentOutOfRangeException(nameof(cap));
        return new(attributable, rate, cap, Math.Min(RoundVnd(attributable * rate), RoundVnd(cap)));
    }

    private static decimal RecalculateDriverLiability(
        decimal attributable,
        ClaimLiabilityCalculationInput input) => input.DriverFaultLevel switch
        {
            DriverFaultLevel.NO_FAULT => 0m,
            DriverFaultLevel.ORDINARY_NEGLIGENCE => Math.Min(
                RoundVnd(attributable * input.OrdinaryNegligenceRate),
                RoundVnd(input.OrdinaryNegligenceCap)),
            DriverFaultLevel.GROSS_NEGLIGENCE => Math.Min(
                RoundVnd(attributable * input.GrossNegligenceRate),
                RoundVnd(input.GrossNegligenceCap)),
            DriverFaultLevel.INTENTIONAL_MISCONDUCT => attributable,
            _ => throw new ArgumentOutOfRangeException(nameof(input.DriverFaultLevel))
        };

    private static decimal[] AllocateResidual(decimal residual, params decimal[] percentages)
    {
        var allocations = percentages
            .Select(percentage => residual * percentage / 100m)
            .Select(decimal.Truncate)
            .ToArray();
        // Inputs used by the service always total 100%. Preserve an omitted
        // tail for backwards-compatible calculator callers rather than
        // looping millions of times to distribute a genuinely unassigned
        // percentage.
        if (percentages.Sum() < 100m) return allocations;
        var remaining = (int)(residual - allocations.Sum());
        var order = percentages
            .Select((percentage, index) => new
            {
                index,
                Fraction = residual * percentage / 100m
                    - decimal.Truncate(residual * percentage / 100m)
            })
            .OrderByDescending(item => item.Fraction)
            .ThenBy(item => item.index)
            .ToArray();
        for (var i = 0; i < remaining && order.Length > 0; i++)
            allocations[order[i % order.Length].index] += 1m;
        return allocations;
    }

    private static decimal[] AllocateByWeight(decimal amount, params decimal[] weights)
    {
        var totalWeight = weights.Sum();
        if (amount == 0m || totalWeight == 0m) return new decimal[weights.Length];

        var allocations = weights
            .Select(weight => decimal.Truncate(amount * weight / totalWeight))
            .ToArray();
        var remaining = (int)(amount - allocations.Sum());
        var order = weights
            .Select((weight, index) => new
            {
                index,
                Fraction = amount * weight / totalWeight
                    - decimal.Truncate(amount * weight / totalWeight)
            })
            .OrderByDescending(item => item.Fraction)
            .ThenBy(item => item.index)
            .ToArray();
        for (var i = 0; i < remaining; i++)
            allocations[order[i % order.Length].index] += 1m;
        return allocations;
    }

    private static decimal RoundVnd(decimal amount) => decimal.Round(amount, 0, MidpointRounding.AwayFromZero);

    private static void EnsureRate(decimal rate, string name)
    {
        if (rate is < 0 or > 1) throw new ArgumentOutOfRangeException(name);
    }
}
