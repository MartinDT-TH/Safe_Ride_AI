using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Services;

namespace SafeRide.UnitTests;

public sealed class MockInsuranceProviderTests
{
    private readonly MockInsuranceProvider _provider = new();

    [Fact]
    public async Task SubmitClaim_AutoApprovesWithinThresholdAndAllCoverageCaps()
    {
        var result = await _provider.SubmitClaimAsync(new InsuranceClaimSubmissionContext(
            10,
            1_500_000m,
            2_000_000m,
            3_000_000m,
            500_000m,
            2_000_000m,
            1_500_000m), CancellationToken.None);

        Assert.Equal(InsuranceClaimStatus.APPROVED, result.Status);
        Assert.Equal(1_500_000m, result.ApprovedAmount);
        Assert.Equal(1_500_000m, result.MaximumApprovableInsuranceAmount);
        Assert.Contains("RequestedAmount", result.RequestPayload);
        Assert.Contains("approvedAmount", result.ResponsePayload);
    }

    [Fact]
    public async Task SubmitClaim_LargeClaimStaysPendingAndPreservesPotentialCoveredAmount()
    {
        var result = await _provider.SubmitClaimAsync(new InsuranceClaimSubmissionContext(
            11,
            8_000_000m,
            10_000_000m,
            7_000_000m,
            1_000_000m,
            5_000_000m,
            2_000_000m), CancellationToken.None);

        Assert.Equal(InsuranceClaimStatus.PENDING, result.Status);
        Assert.Equal(5_000_000m, result.ApprovedAmount);
        Assert.Equal(5_000_000m, result.MaximumApprovableInsuranceAmount);
        Assert.Equal(8_000_000m, result.RequestedAmount);
    }

    [Fact]
    public async Task SubmitClaim_WithoutOptionalInsuranceIsRejected()
    {
        var result = await _provider.SubmitClaimAsync(new InsuranceClaimSubmissionContext(
            12,
            1_000_000m,
            2_000_000m,
            null,
            null,
            5_000_000m,
            2_000_000m), CancellationToken.None);

        Assert.Equal(InsuranceClaimStatus.REJECTED, result.Status);
        Assert.Equal(0m, result.ApprovedAmount);
    }

    [Fact]
    public async Task CalculateAndGetStatus_UseOnlyProvidedCoverageSnapshot()
    {
        var calculated = await _provider.CalculateClaimAsync(new InsuranceClaimCalculationContext(
            13, 9_000_000m, 8_000_000m, 7_000_000m, 1_000_000m, 5_000_000m),
            CancellationToken.None);

        Assert.Equal(5_000_000m, calculated.ApprovedAmount);

        var status = await _provider.GetClaimStatusAsync(new InsuranceClaimStatusContext(
            13, "MOCK-13", InsuranceClaimStatus.APPROVED, 9_000_000m, 6_000_000m,
            8_000_000m, 7_000_000m, 1_000_000m, 5_000_000m),
            CancellationToken.None);

        Assert.Equal(InsuranceClaimStatus.APPROVED, status.Status);
        Assert.Equal(5_000_000m, status.ApprovedAmount);
        Assert.Equal(5_000_000m, status.MaximumApprovableInsuranceAmount);
    }

    [Fact]
    public async Task ReviewClaim_UsesProviderReferenceAndReportsMaximum()
    {
        var result = await _provider.ReviewClaimAsync(new InsuranceClaimReviewContext(
            14, 8_000_000m, 10_000_000m, 7_000_000m, 1_000_000m, 5_000_000m,
            4_000_000m, "MOCK-14-STABLE", "Lower approval reason"), true, CancellationToken.None);

        Assert.Equal(InsuranceClaimStatus.APPROVED, result.Status);
        Assert.Equal("MOCK-14-STABLE", result.Reference);
        Assert.Equal(5_000_000m, result.MaximumApprovableInsuranceAmount);
        Assert.Contains("maximumApprovableInsuranceAmount", result.ResponsePayload);
    }
}
