using System.Text.Json;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Enums;

namespace SafeRide.Infrastructure.Services;

public sealed class MockInsuranceProvider : IInsuranceProvider
{
    public Task<InsuranceCalculationResult> CalculateClaimAsync(
        InsuranceClaimCalculationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requested = Round(Math.Max(0m, context.RequestedAmount));
        var approved = CalculateApprovedAmount(
            requested,
            context.EligibleDamageAmount,
            context.PolicyCoverageAmount,
            context.PolicyDeductibleAmount,
            context.ProviderCoverageLimit);
        var reference = $"MOCK-CALC-{context.ClaimId}-{Guid.NewGuid():N}";
        var requestPayload = JsonSerializer.Serialize(context);
        var responsePayload = JsonSerializer.Serialize(new
        {
            requestedAmount = requested,
            approvedAmount = approved,
            reference
        });

        return Task.FromResult(new InsuranceCalculationResult(
            requested, approved, reference, requestPayload, responsePayload));
    }

    public Task<InsuranceSubmissionResult> SubmitClaimAsync(
        InsuranceClaimSubmissionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requested = Round(Math.Max(0m, context.RequestedAmount));
        var approved = CalculateApprovedAmount(
            requested,
            context.EligibleDamageAmount,
            context.PolicyCoverageAmount,
            context.PolicyDeductibleAmount,
            context.ProviderCoverageLimit);
        var status = approved <= 0m
            ? InsuranceClaimStatus.REJECTED
            : requested <= context.AutoApprovalThreshold
                ? InsuranceClaimStatus.APPROVED
                : InsuranceClaimStatus.PENDING;
        var reference = $"MOCK-{context.ClaimId}-{Guid.NewGuid():N}";
        var requestPayload = JsonSerializer.Serialize(context);
        var responsePayload = JsonSerializer.Serialize(new
        {
            status,
            requestedAmount = requested,
            approvedAmount = approved,
            reference
        });
        var message = status switch
        {
            InsuranceClaimStatus.APPROVED => "Yêu cầu bảo hiểm mô phỏng đã được tự động duyệt.",
            InsuranceClaimStatus.PENDING => "Yêu cầu bảo hiểm mô phỏng đang chờ Staff xem xét.",
            _ => "Yêu cầu bảo hiểm mô phỏng không có phạm vi bảo hiểm hợp lệ."
        };

        return Task.FromResult(new InsuranceSubmissionResult(
            status, reference, requested, approved, message, requestPayload, responsePayload));
    }

    public Task<InsuranceSubmissionResult> GetClaimStatusAsync(
        InsuranceClaimStatusContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requested = Round(Math.Max(0m, context.RequestedAmount));
        var maximumApproved = CalculateApprovedAmount(
            requested,
            context.EligibleDamageAmount,
            context.PolicyCoverageAmount,
            context.PolicyDeductibleAmount,
            context.ProviderCoverageLimit);
        var approved = context.CurrentStatus == InsuranceClaimStatus.APPROVED
            ? Math.Min(Round(Math.Max(0m, context.ApprovedAmount)), maximumApproved)
            : context.CurrentStatus == InsuranceClaimStatus.PENDING
                ? maximumApproved
                : 0m;
        var requestPayload = JsonSerializer.Serialize(context);
        var responsePayload = JsonSerializer.Serialize(new
        {
            status = context.CurrentStatus,
            requestedAmount = requested,
            approvedAmount = approved,
            reference = context.Reference
        });

        return Task.FromResult(new InsuranceSubmissionResult(
            context.CurrentStatus,
            context.Reference,
            requested,
            approved,
            "Đã đồng bộ trạng thái hồ sơ bảo hiểm mô phỏng.",
            requestPayload,
            responsePayload));
    }

    public Task<InsuranceSubmissionResult> ReviewClaimAsync(
        InsuranceClaimReviewContext context,
        bool approve,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var requested = Round(Math.Max(0m, context.RequestedAmount));
        var maximumApproved = CalculateApprovedAmount(
            requested,
            context.EligibleDamageAmount,
            context.PolicyCoverageAmount,
            context.PolicyDeductibleAmount,
            context.ProviderCoverageLimit);
        var approved = approve
            ? Math.Min(Round(Math.Max(0m, context.ApprovedAmount)), maximumApproved)
            : 0m;
        var status = approve && approved > 0m
            ? InsuranceClaimStatus.APPROVED
            : InsuranceClaimStatus.REJECTED;
        var reference = context.Reference.Trim();
        var requestPayload = JsonSerializer.Serialize(context);
        var responsePayload = JsonSerializer.Serialize(new
        {
            status,
            requestedAmount = requested,
            approvedAmount = approved,
            reference,
            reason = context.Reason.Trim()
        });

        return Task.FromResult(new InsuranceSubmissionResult(
            status,
            reference,
            requested,
            approved,
            approve ? "Staff đã duyệt yêu cầu bảo hiểm mô phỏng." : "Staff đã từ chối yêu cầu bảo hiểm mô phỏng.",
            requestPayload,
            responsePayload));
    }

    private static decimal CalculateApprovedAmount(
        decimal requested,
        decimal eligibleDamage,
        decimal? policyCoverage,
        decimal? deductible,
        decimal providerLimit)
    {
        var netPolicyCoverage = Math.Max(0m, (policyCoverage ?? 0m) - (deductible ?? 0m));
        return Round(Math.Min(
            requested,
            Math.Min(Math.Max(0m, eligibleDamage), Math.Min(netPolicyCoverage, Math.Max(0m, providerLimit)))));
    }

    private static decimal Round(decimal amount) =>
        decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
}
