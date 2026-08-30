using System.Text.Json;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Enums;

namespace SafeRide.UnitTests;

public sealed class TripCommissionCalculatorTests
{
    private readonly TripCommissionCalculator _calculator = new();

    [Fact]
    public void CalculateClaimRequest_JsonContractIgnoresDeprecatedManualAllocations()
    {
        var request = JsonSerializer.Deserialize<CalculateClaimRequest>(
            """
            {
              "totalDamageAmount": 10000000,
              "eligibleDamageAmount": 8000000,
              "requestedInsuranceAmount": 7000000,
              "requestedRiskFundAmount": 6000000,
              "isPermanentRiskFundLoss": true,
              "insurancePaymentDestination": "REIMBURSE_RISK_FUND",
              "submitToInsurance": false
            }
            """,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(request);
        Assert.Equal(10_000_000m, request.TotalDamageAmount);
        Assert.Equal(8_000_000m, request.EligibleDamageAmount);
        Assert.Equal(0m, request.RequestedInsuranceAmount);
        Assert.Equal(0m, request.RequestedRiskFundAmount);
        Assert.False(request.IsPermanentRiskFundLoss);
        Assert.Equal(InsurancePaymentDestination.DIRECT_TO_CLAIMANT, request.InsurancePaymentDestination);
        Assert.False(request.SubmitToInsurance);
    }

    [Fact]
    public void Calculate_PlatformFundedPromotion_PreservesDriverEarning()
    {
        var result = _calculator.Calculate(new CommissionCalculationInput(
            100_000m, 20_000m, 0.30m, 0.05m, true));

        Assert.Equal(100_000m, result.CommissionBase);
        Assert.Equal(80_000m, result.CustomerPayableAmount);
        Assert.Equal(70_000m, result.DriverEarning);
        Assert.Equal(10_000m, result.NetPlatformCommission);
        Assert.Equal(500m, result.RiskContribution);
        Assert.Equal(9_500m, result.NetOperatingRevenue);
    }

    [Fact]
    public void Calculate_PromotionExceedsCommission_FloorsOnlyContributionBase()
    {
        var result = _calculator.Calculate(new CommissionCalculationInput(
            100_000m, 40_000m, 0.30m, 0.05m, true));

        Assert.Equal(70_000m, result.DriverEarning);
        Assert.Equal(-10_000m, result.NetPlatformCommission);
        Assert.Equal(0m, result.RiskContribution);
        Assert.Equal(-10_000m, result.NetOperatingRevenue);
    }

    [Theory]
    [InlineData(20_000, 80_000, 10_000, 500)]
    [InlineData(40_000, 60_000, -10_000, 0)]
    [InlineData(120_000, 0, -90_000, 0)]
    public void Calculate_PromotionExpense_IsNotClampedToFareOrGrossCommission(
        decimal promotionExpense,
        decimal expectedCustomerPayable,
        decimal expectedNetCommission,
        decimal expectedRiskContribution)
    {
        var result = _calculator.Calculate(new CommissionCalculationInput(
            100_000m, promotionExpense, 0.30m, 0.05m, true));

        Assert.Equal(promotionExpense, result.PromotionExpense);
        Assert.Equal(30_000m, result.GrossPlatformCommission);
        Assert.Equal(70_000m, result.DriverEarning);
        Assert.Equal(expectedCustomerPayable, result.CustomerPayableAmount);
        Assert.Equal(expectedNetCommission, result.NetPlatformCommission);
        Assert.Equal(expectedRiskContribution, result.RiskContribution);
    }

    [Fact]
    public void Calculate_SafetyCancelledTrip_DoesNotContribute()
    {
        var result = _calculator.Calculate(new CommissionCalculationInput(
            50_000m, 0m, 0.30m, 0.05m, false));

        Assert.Equal(15_000m, result.NetPlatformCommission);
        Assert.Equal(0m, result.RiskContribution);
    }

    [Fact]
    public void CalculateComponentAware_CommissionsOnlyFareAndPaysAllDriverComponents()
    {
        var result = _calculator.CalculateComponentAware(
            new ComponentAwareCommissionCalculationInput(
                GrossFare: 130_000m,
                FareComponent: 100_000m,
                LongDistanceComponent: 30_000m,
                SnapshotPromotionDiscount: 20_000m,
                LongPickupCompensation: 15_000m,
                PlatformCommissionRate: 0.30m,
                RiskReserveRate: 0.05m,
                IsRiskContributionEligible: true));

        Assert.Equal(110_000m, result.CustomerPayableAmount);
        Assert.Equal(100_000m, result.CommissionBase);
        Assert.Equal(30_000m, result.GrossPlatformCommission);
        Assert.Equal(70_000m, result.DriverFareEarning);
        Assert.Equal(30_000m, result.LongDistanceEarning);
        Assert.Equal(15_000m, result.LongPickupCompensation);
        Assert.Equal(115_000m, result.DriverPayout);
        Assert.Equal(20_000m, result.PromotionExpense);
        Assert.Equal(10_000m, result.NetPlatformCommission);
        Assert.Equal(500m, result.RiskContribution);
        Assert.Equal(-5_500m, result.NetOperatingRevenue);
    }

    [Fact]
    public void CalculateComponentAware_DiscountIsCappedByGrossFareWithoutReducingPayout()
    {
        var result = _calculator.CalculateComponentAware(
            new ComponentAwareCommissionCalculationInput(
                100_000m, 80_000m, 20_000m, 150_000m, 0m, 0.25m, 0.10m, true));

        Assert.Equal(150_000m, result.SnapshotPromotionDiscount);
        Assert.Equal(100_000m, result.AppliedPromotionDiscount);
        Assert.Equal(0m, result.CustomerPayableAmount);
        Assert.Equal(80_000m, result.DriverPayout);
        Assert.Equal(0m, result.RiskContribution);
        Assert.Equal(-80_000m, result.NetOperatingRevenue);
    }

    [Fact]
    public void CalculateComponentAware_RejectsUnreconciledComponents()
    {
        Assert.Throws<ArgumentException>(() => _calculator.CalculateComponentAware(
            new ComponentAwareCommissionCalculationInput(
                100_000m, 80_000m, 10_000m, 0m, 0m, 0.30m, 0.05m, true)));
    }

    [Fact]
    public void CalculateComponentAware_RoundsCommissionAwayFromZeroBeforeSummingPayout()
    {
        var result = _calculator.CalculateComponentAware(
            new ComponentAwareCommissionCalculationInput(
                130_005m, 100_005m, 30_000m, 0m, 7_500m, 0.10m, 0m, false));

        Assert.Equal(10_001m, result.GrossPlatformCommission);
        Assert.Equal(90_004m, result.DriverFareEarning);
        Assert.Equal(127_504m, result.DriverPayout);
        Assert.Equal(
            result.DriverFareEarning
                + result.LongDistanceEarning
                + result.LongPickupCompensation,
            result.DriverPayout);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(40, 0.1, 4000000)]
    [InlineData(100, 0.1, 5000000)]
    public void CalculateDriverLiability_OrdinaryNegligence_AppliesFaultRateAndCap(
        decimal faultPercentage, decimal negligenceRate, decimal expected)
    {
        var result = _calculator.CalculateDriverLiability(new DriverLiabilityCalculationInput(
            100_000_000m, faultPercentage, DriverFaultLevel.ORDINARY_NEGLIGENCE,
            negligenceRate, 5_000_000m, 0.5m, 20_000_000m));

        Assert.Equal(expected, result.LiabilityAmount);
    }

    [Theory]
    [InlineData(25, 25_000_000)]
    [InlineData(50, 50_000_000)]
    [InlineData(100, 100_000_000)]
    public void CalculateDriverLiability_IntentionalMisconduct_RecoversOnlyDriverAttributableDamage(
        decimal faultPercentage,
        decimal expected)
    {
        var result = _calculator.CalculateDriverLiability(new DriverLiabilityCalculationInput(
            100_000_000m, faultPercentage, DriverFaultLevel.INTENTIONAL_MISCONDUCT,
            0.1m, 5_000_000m, 0.5m, 20_000_000m));

        Assert.Equal(expected, result.DriverAttributableEligibleDamage);
        Assert.Equal(expected, result.LiabilityAmount);
    }

    [Fact]
    public void CalculateLiabilities_MixedFault_DerivesAllPartyObligationsFromEligibleDamage()
    {
        var result = _calculator.CalculateLiabilities(new ClaimLiabilityCalculationInput(
            100_000_000m,
            40m,
            30m,
            10m,
            DriverFaultLevel.ORDINARY_NEGLIGENCE,
            0.20m,
            20_000_000m,
            0.50m,
            50_000_000m));

        Assert.Equal(40_000_000m, result.Driver.DriverAttributableEligibleDamage);
        Assert.Equal(8_000_000m, result.Driver.LiabilityAmount);
        Assert.Equal(30_000_000m, result.CustomerLiabilityAmount);
        Assert.Equal(10_000_000m, result.ThirdPartyLiabilityAmount);
        Assert.Equal(48_000_000m, result.TotalRecoverableLiabilityAmount);
    }

    [Fact]
    public void CalculateLiabilities_GrossNegligence_AppliesConfiguredCap()
    {
        var result = _calculator.CalculateLiabilities(new ClaimLiabilityCalculationInput(
            100_000_000m,
            80m,
            0m,
            0m,
            DriverFaultLevel.GROSS_NEGLIGENCE,
            0.20m,
            20_000_000m,
            0.75m,
            25_000_000m));

        Assert.Equal(80_000_000m, result.Driver.DriverAttributableEligibleDamage);
        Assert.Equal(25_000_000m, result.Driver.LiabilityAmount);
    }

    [Fact]
    public void CalculateLiabilities_ThirdPartyFaultDoesNotCreateDriverLiability()
    {
        var result = _calculator.CalculateLiabilities(new ClaimLiabilityCalculationInput(
            100_000_000m,
            0m,
            0m,
            100m,
            DriverFaultLevel.NO_FAULT,
            0.20m,
            20_000_000m,
            0.50m,
            50_000_000m));

        Assert.Equal(0m, result.Driver.DriverAttributableEligibleDamage);
        Assert.Equal(0m, result.Driver.LiabilityAmount);
        Assert.Equal(100_000_000m, result.ThirdPartyLiabilityAmount);
    }

    [Fact]
    public void CalculateLiabilities_CustomerInsuranceThenSystemInsurance_UsesRequiredWaterfall()
    {
        var result = _calculator.CalculateLiabilities(new ClaimLiabilityCalculationInput(
            10_000_000m, 30m, 70m, 0m,
            DriverFaultLevel.ORDINARY_NEGLIGENCE,
            0.50m, 20_000_000m, 0.75m, 50_000_000m,
            InsurancePaidAmount: 2_000_000m,
            CustomerInsuranceAppliedAmount: 6_000_000m));

        Assert.Equal(7_000_000m, result.CustomerGrossExposure);
        Assert.Equal(3_000_000m, result.DriverGrossExposure);
        Assert.Equal(1_000_000m, result.CustomerExposureAfterOwnInsurance);
        Assert.Equal(500_000m, result.CustomerSystemInsuranceBenefit);
        Assert.Equal(1_500_000m, result.DriverSystemInsuranceBenefit);
        Assert.Equal(2_000_000m, result.ResidualUninsuredDamage);
        Assert.Equal(1_500_000m, result.Driver.DriverAttributableResidualDamage);
        Assert.Equal(750_000m, result.Driver.LiabilityAmount);
        Assert.Equal(500_000m, result.CustomerAttributableResidualDamage);
        Assert.Equal(500_000m, result.CustomerLiabilityAmount);
        Assert.Equal(0m, result.ThirdPartyLiabilityAmount);
        Assert.Equal(1_250_000m, result.TotalRecoverableLiabilityAmount);
    }

    [Fact]
    public void CalculateLiabilities_NoInsurance_UsesTheFullEligibleLossAsResidual()
    {
        var result = _calculator.CalculateLiabilities(new ClaimLiabilityCalculationInput(
            10_000_000m, 30m, 70m, 0m,
            DriverFaultLevel.ORDINARY_NEGLIGENCE,
            0.50m, 20_000_000m, 0.75m, 50_000_000m));

        Assert.Equal(10_000_000m, result.ResidualUninsuredDamage);
        Assert.Equal(3_000_000m, result.DriverAttributableResidualDamage);
        Assert.Equal(7_000_000m, result.CustomerLiabilityAmount);
    }

    [Fact]
    public void CalculateLiabilities_CustomerFault_SystemInsuranceLeavesOnlyResidualCustomerObligation()
    {
        var result = _calculator.CalculateLiabilities(new ClaimLiabilityCalculationInput(
            10_000_000m, 0m, 100m, 0m,
            DriverFaultLevel.NO_FAULT,
            0.50m, 20_000_000m, 0.75m, 50_000_000m,
            InsurancePaidAmount: 4_000_000m));

        Assert.Equal(6_000_000m, result.ResidualUninsuredDamage);
        Assert.Equal(6_000_000m, result.CustomerLiabilityAmount);
    }

    [Fact]
    public void CalculateLiabilities_ThirdPartyFault_IsOutsideSystemInsuranceParticipantScope()
    {
        var result = _calculator.CalculateLiabilities(new ClaimLiabilityCalculationInput(
            10_000_000m, 0m, 0m, 100m,
            DriverFaultLevel.NO_FAULT,
            0.50m, 20_000_000m, 0.75m, 50_000_000m,
            InsurancePaidAmount: 0m));

        Assert.Equal(10_000_000m, result.ResidualUninsuredDamage);
        Assert.Equal(10_000_000m, result.ThirdPartyLiabilityAmount);
    }

    [Fact]
    public void CalculateLiabilities_FullInsuranceLeavesFaultHistoryButNoFinancialExposure()
    {
        var result = _calculator.CalculateLiabilities(new ClaimLiabilityCalculationInput(
            10_000_000m, 30m, 70m, 0m,
            DriverFaultLevel.ORDINARY_NEGLIGENCE,
            0.50m, 20_000_000m, 0.75m, 50_000_000m,
            InsurancePaidAmount: 10_000_000m));

        Assert.Equal(0m, result.ResidualUninsuredDamage);
        Assert.Equal(0m, result.Driver.DriverAttributableResidualDamage);
        Assert.Equal(0m, result.Driver.LiabilityAmount);
        Assert.Equal(0m, result.CustomerLiabilityAmount);
        Assert.Equal(0m, result.ThirdPartyLiabilityAmount);
        Assert.Equal(0m, result.TotalRecoverableLiabilityAmount);
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(0, 100)]
    public void CalculateLiabilities_VehicleOrObjectiveShareIsNotAssignedToHumans(
        decimal vehiclePercentage,
        decimal objectivePercentage)
    {
        var result = _calculator.CalculateLiabilities(new ClaimLiabilityCalculationInput(
            10_000_000m, 0m, 0m, 0m,
            DriverFaultLevel.NO_FAULT,
            0.50m, 20_000_000m, 0.75m, 50_000_000m,
            InsurancePaidAmount: 0m,
            VehicleFailurePercentage: vehiclePercentage,
            ObjectiveCausePercentage: objectivePercentage));

        Assert.Equal(10_000_000m, result.ResidualUninsuredDamage);
        Assert.Equal(10_000_000m, result.VehicleObjectiveResidualAmount);
        Assert.Equal(0m, result.Driver.LiabilityAmount);
        Assert.Equal(0m, result.CustomerLiabilityAmount);
        Assert.Equal(0m, result.ThirdPartyLiabilityAmount);
        Assert.Equal(0m, result.TotalRecoverableLiabilityAmount);
    }

    [Fact]
    public void CalculateLiabilities_CustomerInsuranceCannotExceedCustomerGrossExposure()
    {
        var input = new ClaimLiabilityCalculationInput(
            10_000_000m, 30m, 70m, 0m,
            DriverFaultLevel.ORDINARY_NEGLIGENCE,
            0.50m, 20_000_000m, 0.75m, 50_000_000m,
            CustomerInsuranceAppliedAmount: 7_000_001m);

        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.CalculateLiabilities(input));
    }

    [Fact]
    public void CalculateLiabilities_FullCustomerInsuranceLeavesOnlyDriverForSystemInsurance()
    {
        var result = _calculator.CalculateLiabilities(new ClaimLiabilityCalculationInput(
            10_000_000m, 30m, 70m, 0m,
            DriverFaultLevel.ORDINARY_NEGLIGENCE,
            0.50m, 20_000_000m, 0.75m, 50_000_000m,
            InsurancePaidAmount: 1_500_000m,
            CustomerInsuranceAppliedAmount: 7_000_000m));

        Assert.Equal(0m, result.CustomerExposureAfterOwnInsurance);
        Assert.Equal(0m, result.CustomerSystemInsuranceBenefit);
        Assert.Equal(1_500_000m, result.DriverSystemInsuranceBenefit);
        Assert.Equal(1_500_000m, result.DriverRemainingExposureBeforeRateCap);
        Assert.Equal(750_000m, result.Driver.LiabilityAmount);
    }

    [Fact]
    public void CalculateLiabilities_PartialSystemInsurance_IsProportionalToRemainingParticipantExposure()
    {
        var result = _calculator.CalculateLiabilities(new ClaimLiabilityCalculationInput(
            10_000_000m, 30m, 70m, 0m,
            DriverFaultLevel.ORDINARY_NEGLIGENCE,
            0.50m, 20_000_000m, 0.75m, 50_000_000m,
            InsurancePaidAmount: 1_000_000m,
            CustomerInsuranceAppliedAmount: 6_000_000m));

        Assert.Equal(250_000m, result.CustomerSystemInsuranceBenefit);
        Assert.Equal(750_000m, result.DriverSystemInsuranceBenefit);
        Assert.Equal(750_000m, result.CustomerFinalExposure);
        Assert.Equal(2_250_000m, result.DriverRemainingExposureBeforeRateCap);
    }

    [Fact]
    public void CalculateLiabilities_SystemInsuranceFullyCoversPostCustomerInsuranceParticipants()
    {
        var result = _calculator.CalculateLiabilities(new ClaimLiabilityCalculationInput(
            10_000_000m, 30m, 70m, 0m,
            DriverFaultLevel.ORDINARY_NEGLIGENCE,
            0.50m, 20_000_000m, 0.75m, 50_000_000m,
            InsurancePaidAmount: 4_000_000m,
            CustomerInsuranceAppliedAmount: 6_000_000m));

        Assert.Equal(0m, result.ResidualUninsuredDamage);
        Assert.Equal(0m, result.CustomerFinalExposure);
        Assert.Equal(0m, result.DriverRemainingExposureBeforeRateCap);
        Assert.Equal(0m, result.Driver.LiabilityAmount);
    }

    [Fact]
    public void CalculateLiabilities_SystemInsuranceCannotExceedRemainingParticipantExposure()
    {
        var input = new ClaimLiabilityCalculationInput(
            10_000_000m, 30m, 70m, 0m,
            DriverFaultLevel.ORDINARY_NEGLIGENCE,
            0.50m, 20_000_000m, 0.75m, 50_000_000m,
            InsurancePaidAmount: 4_000_001m,
            CustomerInsuranceAppliedAmount: 6_000_000m);

        Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.CalculateLiabilities(input));
    }
}
