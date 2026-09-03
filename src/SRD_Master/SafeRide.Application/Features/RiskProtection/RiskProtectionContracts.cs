using System.Text.Json.Serialization;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.RiskProtection;

public sealed record StoredSafetyTerminationEvidence(
    string EvidenceUrl,
    string StoragePublicId,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes);

// EvidenceUrl is a Staff-supplied external audit reference. It is not an uploaded
// file and must never be represented as malware-scanned or Clean.
public sealed record ManualRefundConfirmationRequest(
    string PaymentReference,
    string EvidenceUrl,
    string IdempotencyKey,
    string RowVersion);

public sealed record SafetyPaymentReconciliationResponse(
    long TripId,
    decimal CustomerPayableAmount,
    decimal SuccessfulPaymentAmount,
    decimal RemainingPayableAmount,
    decimal RefundObligationAmount,
    decimal DriverCreditedAmount,
    SafetyPaymentReconciliationStatus Status,
    long? RefundId,
    ManualRefundStatus? RefundStatus,
    string RowVersion);

public sealed record ManualRefundQueueItemResponse(
    long RefundId,
    long TripId,
    long PaymentId,
    decimal Amount,
    ManualRefundStatus Status,
    string? PaymentReference,
    string? EvidenceUrl,
    Guid? RefundedByUserId,
    DateTime CreatedAtUtc,
    DateTime? RefundedAtUtc,
    string RowVersion);

public sealed record CommissionCalculationInput(
    decimal ActualFare,
    decimal PromotionExpense,
    decimal PlatformCommissionRate,
    decimal RiskReserveRate,
    bool IsRiskContributionEligible);

public sealed record CommissionCalculationResult(
    decimal CommissionBase,
    decimal PromotionExpense,
    decimal CustomerPayableAmount,
    decimal PlatformCommissionRate,
    decimal GrossPlatformCommission,
    decimal DriverEarning,
    decimal NetPlatformCommission,
    decimal RiskReserveRate,
    decimal RiskContribution,
    decimal NetOperatingRevenue);

public sealed record ComponentAwareCommissionCalculationInput(
    decimal GrossFare,
    decimal FareComponent,
    decimal LongDistanceComponent,
    decimal SnapshotPromotionDiscount,
    decimal LongPickupCompensation,
    decimal PlatformCommissionRate,
    decimal RiskReserveRate,
    bool IsRiskContributionEligible);

public sealed record ComponentAwareCommissionCalculationResult(
    decimal GrossFare,
    decimal FareComponent,
    decimal LongDistanceComponent,
    decimal SnapshotPromotionDiscount,
    decimal AppliedPromotionDiscount,
    decimal CustomerPayableAmount,
    decimal CommissionBase,
    decimal PlatformCommissionRate,
    decimal GrossPlatformCommission,
    decimal DriverFareEarning,
    decimal LongDistanceEarning,
    decimal LongPickupCompensation,
    decimal DriverPayout,
    decimal PromotionExpense,
    decimal NetPlatformCommission,
    decimal RiskReserveRate,
    decimal RiskContribution,
    decimal NetOperatingRevenue);

public sealed record DriverLiabilityCalculationInput(
    decimal EligibleDamage,
    decimal DriverFaultPercentage,
    DriverFaultLevel FaultLevel,
    decimal OrdinaryNegligenceRate,
    decimal OrdinaryNegligenceCap,
    decimal GrossNegligenceRate,
    decimal GrossNegligenceCap);

public sealed record DriverLiabilityCalculationResult(
    decimal DriverAttributableEligibleDamage,
    decimal AppliedRate,
    decimal? AppliedCap,
    decimal LiabilityAmount)
{
    // A1 applies the policy rate/cap to the post-insurance residual. Keep the
    // historical property name for API/test compatibility and expose the
    // unambiguous alias for new callers.
    public decimal DriverAttributableResidualDamage => DriverAttributableEligibleDamage;
}

public sealed record ClaimLiabilityCalculationInput(
    decimal EligibleDamage,
    decimal DriverFaultPercentage,
    decimal CustomerFaultPercentage,
    decimal ThirdPartyFaultPercentage,
    DriverFaultLevel DriverFaultLevel,
    decimal OrdinaryNegligenceRate,
    decimal OrdinaryNegligenceCap,
    decimal GrossNegligenceRate,
    decimal GrossNegligenceCap,
    decimal InsurancePaidAmount = 0m,
    decimal VehicleFailurePercentage = 0m,
    decimal ObjectiveCausePercentage = 0m,
    decimal CustomerInsuranceAppliedAmount = 0m);

public sealed record ClaimLiabilityCalculationResult(
    DriverLiabilityCalculationResult Driver,
    decimal CustomerLiabilityAmount,
    decimal ThirdPartyLiabilityAmount,
    decimal TotalRecoverableLiabilityAmount)
{
    public decimal ResidualUninsuredDamage { get; init; }
    public decimal CustomerGrossExposure { get; init; }
    public decimal DriverGrossExposure { get; init; }
    public decimal ThirdPartyGrossExposure { get; init; }
    public decimal VehicleObjectiveGrossExposure { get; init; }
    public decimal CustomerInsuranceAppliedAmount { get; init; }
    public decimal CustomerInsuranceBenefitToCustomer { get; init; }
    public decimal CustomerInsuranceExcessAppliedToOtherLoss { get; init; }
    public decimal CustomerInsuranceBenefitToDriver { get; init; }
    public decimal CustomerInsuranceUnallocatedCategoryReduction { get; init; }
    public decimal CustomerExposureAfterOwnInsurance { get; init; }
    public decimal DriverExposureBeforeSystemInsurance { get; init; }
    public decimal RemainingLossAfterCustomerInsurance { get; init; }
    public decimal ParticipantExposureBeforeSystemInsurance { get; init; }
    public decimal SystemInsuranceApprovedAmount { get; init; }
    public decimal CustomerSystemInsuranceBenefit { get; init; }
    public decimal DriverSystemInsuranceBenefit { get; init; }
    public decimal CustomerFinalExposure { get; init; }
    public decimal DriverRemainingExposureBeforeRateCap { get; init; }
    public decimal DriverAttributableResidualDamage => Driver.DriverAttributableResidualDamage;
    public decimal CustomerAttributableResidualDamage { get; init; }
    public decimal ThirdPartyAttributableResidualDamage { get; init; }
    public decimal VehicleObjectiveResidualAmount { get; init; }
}

public sealed record RiskProtectionPolicyResponse(
    long Id,
    DateTime EffectiveFromUtc,
    decimal BasePlatformCommissionRate,
    decimal RiskReserveRate,
    decimal DefaultProtectionLimit,
    decimal DriverOrdinaryNegligenceRate,
    decimal DriverOrdinaryNegligenceCap,
    decimal DriverGrossNegligenceRate,
    decimal DriverGrossNegligenceCap,
    decimal MockInsuranceCoverageLimit,
    decimal ClaimAutoApprovalThreshold,
    bool RiskFundEnabled,
    string ChangeReason,
    DateTime CreatedAtUtc);

public sealed record CreateRiskProtectionPolicyRequest(
    DateTime EffectiveFromUtc,
    decimal BasePlatformCommissionRate,
    decimal RiskReserveRate,
    decimal DefaultProtectionLimit,
    decimal DriverOrdinaryNegligenceRate,
    decimal DriverOrdinaryNegligenceCap,
    decimal DriverGrossNegligenceRate,
    decimal DriverGrossNegligenceCap,
    decimal MockInsuranceCoverageLimit,
    decimal ClaimAutoApprovalThreshold,
    bool RiskFundEnabled,
    string ChangeReason);

// EvidenceUrl remains in the JSON contract for compatibility but production
// service code rejects it; trusted evidence must use the multipart upload path.
public sealed record PreTripVehicleCheckRequest(
    bool BrakeResponsePassed,
    bool FrontRearLightsPassed,
    bool TurnSignalsPassed,
    bool VisibleTiresPassed,
    bool DashboardWarningPassed,
    bool WindshieldVisibilityPassed,
    bool NoMajorVisibleIssue,
    VehicleFaultType? FaultType,
    string? Note,
    string? EvidenceUrl);

public sealed record StoredPreTripVehicleCheckEvidence(
    string FileUrl,
    string? StoragePublicId,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes);

public sealed record PreTripVehicleCheckResponse(
    long Id,
    long TripId,
    Guid DriverId,
    bool BrakeResponsePassed,
    bool FrontRearLightsPassed,
    bool TurnSignalsPassed,
    bool VisibleTiresPassed,
    bool DashboardWarningPassed,
    bool WindshieldVisibilityPassed,
    bool NoMajorVisibleIssue,
    PreTripCheckResult Result,
    VehicleFaultType? FaultType,
    string? Note,
    string? EvidenceUrl,
    string? EvidenceOriginalFileName,
    string? EvidenceContentType,
    long? EvidenceFileSizeBytes,
    DateTime CheckedAtUtc);

public sealed record SafetyReportRequest(
    SafetyReportType ReportType,
    string ReasonCode,
    string Description,
    decimal? Latitude,
    decimal? Longitude,
    bool EscalationRequested);

public sealed record SafetyReportResponse(
    long Id,
    long TripId,
    SafetyReportType ReportType,
    string ReasonCode,
    bool EscalationRequested,
    long? SosAlertId,
    DateTime CreatedAtUtc);

public sealed record CreateAccidentRequest(
    AccidentCategory Category,
    DateTime OccurredAtUtc,
    decimal? Latitude,
    decimal? Longitude,
    string Description,
    string? PoliceReportReference);

public sealed record AddAccidentEvidenceRequest(
    AccidentEvidenceType EvidenceType,
    string FileUrl,
    string? OriginalFileName,
    string ContentType,
    string? StoragePublicId,
    long? FileSizeBytes,
    DateTime? CapturedAtUtc,
    decimal? Latitude,
    decimal? Longitude,
    string? Description);

public sealed record LiabilityDisputeRequest(
    string Reason,
    IReadOnlyCollection<long> EvidenceIds);

public sealed record AccidentQueueFilter(
    AccidentStatus? Status,
    AccidentCategory? Category,
    long? TripId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    int Limit = 100);

public sealed record LiabilityCauseRequest(
    AccidentRootCause RootCause,
    ResponsiblePartyType ResponsibleParty,
    decimal Percentage);

public sealed record LiabilityAssessmentRequest(
    decimal DriverFaultPercentage,
    decimal CustomerFaultPercentage,
    decimal ThirdPartyFaultPercentage,
    decimal VehicleFailurePercentage,
    decimal ObjectiveCausePercentage,
    DriverFaultLevel DriverFaultLevel,
    VehicleDefectAwareness VehicleDefectAwareness,
    IReadOnlyCollection<LiabilityCauseRequest> Causes,
    string? RowVersion = null);

public sealed record CalculateClaimRequest(
    decimal TotalDamageAmount,
    decimal EligibleDamageAmount,
    // Deprecated compatibility fields. The server now derives insurance and
    // Risk Fund amounts from the immutable snapshots and confirmed facts.
    [property: JsonIgnore] decimal RequestedInsuranceAmount = 0m,
    [property: JsonIgnore] decimal RequestedRiskFundAmount = 0m,
    [property: JsonIgnore] bool IsPermanentRiskFundLoss = false,
    string? RowVersion = null,
    [property: JsonIgnore] InsurancePaymentDestination InsurancePaymentDestination = InsurancePaymentDestination.DIRECT_TO_CLAIMANT,
    bool SubmitToInsurance = true,
    decimal CustomerInsuranceAppliedAmount = 0m,
    string? CustomerInsuranceReference = null,
    string? CustomerInsuranceNote = null);

public sealed record InsuranceReviewRequest(
    decimal ApprovedAmount,
    string? Reference = null,
    string? Reason = null,
    string? RowVersion = null,
    InsurancePaymentDestination InsurancePaymentDestination = InsurancePaymentDestination.DIRECT_TO_CLAIMANT);

public sealed record ClaimFundingRequest(string RowVersion);

public sealed record TrustedClaimEvidence(
    string EvidenceUrl,
    string StoragePublicId,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes);

public sealed record ClaimRecoveryRequest(
    RecoverySourceType SourceType,
    string PayerReference,
    decimal Amount,
    string PaymentReference,
    TrustedClaimEvidence Evidence,
    string IdempotencyKey,
    string RowVersion);

public sealed record ClaimWriteOffRequest(
    decimal Amount,
    string Reason,
    TrustedClaimEvidence Evidence,
    string IdempotencyKey,
    string RowVersion);

public sealed record CloseClaimRequest(string RowVersion);

// EvidenceUrl is an Admin-supplied external audit reference. It does not cross
// the file-upload scanner boundary and must not be labelled Clean.
public sealed record RiskFundMutationRequest(
    decimal Amount,
    LedgerDirection Direction,
    string Reason,
    string ExternalReference,
    string EvidenceUrl,
    string IdempotencyKey);

public sealed record RiskFundDashboardResponse(
    decimal CurrentBalance,
    decimal TotalContributions,
    decimal ClaimAdvances,
    decimal ClaimPayouts,
    decimal TotalRecoveries,
    decimal OutstandingRecoveries,
    decimal AdjustmentCredits,
    decimal AdjustmentDebits,
    decimal OutstandingExposure,
    int PendingInvestigationClaims,
    int PendingFundingClaims);

public sealed record RiskFundTransactionResponse(
    long Id,
    RiskFundTransactionType TransactionType,
    LedgerDirection Direction,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    long? TripId,
    long? ProtectionClaimId,
    long? ClaimRecoveryId,
    Guid? PerformedByUserId,
    string? ExternalReference,
    string? EvidenceUrl,
    string Reason,
    string IdempotencyKey,
    DateTime CreatedAtUtc);

public sealed record RiskFundTransactionPageResponse(
    IReadOnlyList<RiskFundTransactionResponse> Items,
    bool HasNextPage,
    DateTime? NextCursorCreatedAtUtc,
    long? NextCursorId);

public sealed record RiskFundMutationResponse(
    bool Applied,
    RiskFundTransactionResponse Transaction);

public sealed record AccidentEvidenceResponse(
    long Id,
    Guid UploadedByUserId,
    AccidentEvidenceType EvidenceType,
    string FileUrl,
    string? OriginalFileName,
    string ContentType,
    long? FileSizeBytes,
    DateTime? CapturedAtUtc,
    decimal? Latitude,
    decimal? Longitude,
    string? Description,
    DateTime CreatedAtUtc);

public sealed record AccidentResponse(
    long Id,
    long TripId,
    Guid ReportedByUserId,
    AccidentCategory Category,
    AccidentStatus Status,
    DateTime OccurredAtUtc,
    decimal? Latitude,
    decimal? Longitude,
    string Description,
    string? PoliceReportReference,
    DateTime CreatedAtUtc,
    long? ClaimId,
    ProtectionClaimStatus? ClaimStatus,
    IReadOnlyList<AccidentEvidenceResponse>? Evidence = null,
    LiabilityAssessmentResponse? LiabilityAssessment = null,
    ProtectionClaimResponse? Claim = null);

public sealed record LiabilityCauseResponse(
    AccidentRootCause RootCause,
    ResponsiblePartyType ResponsibleParty,
    decimal Percentage);

public sealed record LiabilityAssessmentResponse(
    long Id,
    decimal DriverFaultPercentage,
    decimal CustomerFaultPercentage,
    decimal ThirdPartyFaultPercentage,
    decimal VehicleFailurePercentage,
    decimal ObjectiveCausePercentage,
    DriverFaultLevel DriverFaultLevel,
    VehicleDefectAwareness VehicleDefectAwareness,
    LiabilityAssessmentStatus Status,
    Guid? ConfirmedByUserId,
    DateTime? ConfirmedAtUtc,
    string? DisputeReason,
    Guid? DisputedByUserId,
    DateTime? DisputedAtUtc,
    IReadOnlyList<long> DisputeEvidenceIds,
    string RowVersion,
    IReadOnlyList<LiabilityCauseResponse> Causes);

public sealed record ProtectionClaimResponse(
    long Id,
    long AccidentReportId,
    string RowVersion,
    string? AssessmentRowVersion,
    ProtectionClaimStatus Status,
    InsuranceClaimStatus InsuranceStatus,
    InsurancePaymentDestination InsurancePaymentDestination,
    decimal InsuranceRequestedAmount,
    decimal TotalDamageAmount,
    decimal EligibleDamageAmount,
    decimal InsuranceApprovedAmount,
    decimal InsurancePaidDirectToClaimant,
    decimal InsuranceReimbursedToRiskFund,
    decimal RiskFundAdvanceAmount,
    decimal RiskFundPermanentLossAmount,
    decimal DriverLiabilityAmount,
    decimal CustomerLiabilityAmount,
    decimal ThirdPartyLiabilityAmount,
    decimal TotalPaidToClaimant,
    decimal RecoveredAmount,
    decimal OutstandingRecoveryAmount,
    decimal WrittenOffAdvanceAmount,
    decimal ActualRecoverableFundExposure,
    bool IsReconciled,
    decimal InsuranceEligibleAmount = 0m,
    decimal ResidualUninsuredDamage = 0m,
    decimal DriverAttributableResidualDamage = 0m,
    decimal CustomerAttributableResidualDamage = 0m,
    decimal ThirdPartyAttributableResidualDamage = 0m,
    decimal VehicleObjectiveResidualAmount = 0m,
    decimal RiskFundRequiredAmount = 0m,
    decimal MaximumApprovableInsuranceAmount = 0m,
    decimal RecommendedInsuranceApprovalAmount = 0m,
    decimal MockInsuranceCoverageLimit = 0m,
    string? InsuranceReference = null,
    decimal CustomerInsuranceAppliedAmount = 0m,
    string? CustomerInsuranceReference = null,
    DateTime? CustomerInsuranceConfirmedAtUtc = null,
    string? CustomerInsuranceNote = null,
    decimal CustomerGrossExposure = 0m,
    decimal DriverGrossExposure = 0m,
    decimal ThirdPartyGrossExposure = 0m,
    decimal VehicleObjectiveGrossExposure = 0m,
    decimal CustomerExposureAfterOwnInsurance = 0m,
    decimal DriverExposureBeforeSystemInsurance = 0m,
    decimal SystemInsuranceCoveredExposureRemaining = 0m,
    decimal SystemInsuranceMaximumAmount = 0m,
    decimal SystemInsuranceApprovedAmount = 0m,
    decimal CustomerSystemInsuranceBenefit = 0m,
    decimal DriverSystemInsuranceBenefit = 0m,
    decimal CustomerFinalExposure = 0m,
    decimal DriverRemainingExposureBeforeRateCap = 0m,
    decimal SystemInsuranceCoverageLimitSnapshot = 0m,
    string SystemInsuranceProvider = "MockInsuranceProvider",
    string SystemInsuranceEvaluationReason = "NOT_EVALUATED",
    decimal CustomerInsuranceBenefitToCustomer = 0m,
    decimal CustomerInsuranceExcessAppliedToOtherLoss = 0m,
    decimal CustomerInsuranceBenefitToDriver = 0m,
    decimal CustomerInsuranceUnallocatedCategoryReduction = 0m,
    decimal RemainingLossAfterCustomerInsurance = 0m);

public sealed record DriverLiabilityResponse(
    long Id,
    long ProtectionClaimId,
    decimal DriverAttributableEligibleDamage,
    DriverFaultLevel FaultLevel,
    decimal ConfirmedAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    DriverLiabilityStatus Status,
    IReadOnlyList<ClaimRecoveryHistoryResponse> Recoveries,
    long? AccidentReportId = null,
    ProtectionClaimStatus? ClaimStatus = null);

public sealed record ClaimRecoveryHistoryResponse(
    long Id,
    RecoverySourceType SourceType,
    decimal Amount,
    string MaskedPaymentReference,
    DateTime RecordedAtUtc);

public sealed record InsuranceCalculationResult(
    decimal RequestedAmount,
    decimal ApprovedAmount,
    string Reference,
    string RequestPayload,
    string ResponsePayload,
    decimal MaximumApprovableInsuranceAmount = 0m);

public sealed record InsuranceSubmissionResult(
    InsuranceClaimStatus Status,
    string Reference,
    decimal RequestedAmount,
    decimal ApprovedAmount,
    string Message,
    string RequestPayload,
    string ResponsePayload,
    decimal MaximumApprovableInsuranceAmount = 0m);

public sealed record InsuranceClaimSubmissionContext(
    long ClaimId,
    decimal RequestedAmount,
    decimal EligibleDamageAmount,
    decimal? PolicyCoverageAmount,
    decimal? PolicyDeductibleAmount,
    decimal ProviderCoverageLimit,
    decimal AutoApprovalThreshold);

public sealed record InsuranceClaimCalculationContext(
    long ClaimId,
    decimal RequestedAmount,
    decimal EligibleDamageAmount,
    decimal? PolicyCoverageAmount,
    decimal? PolicyDeductibleAmount,
    decimal ProviderCoverageLimit);

public sealed record InsuranceClaimStatusContext(
    long ClaimId,
    string Reference,
    InsuranceClaimStatus CurrentStatus,
    decimal RequestedAmount,
    decimal ApprovedAmount,
    decimal EligibleDamageAmount,
    decimal? PolicyCoverageAmount,
    decimal? PolicyDeductibleAmount,
    decimal ProviderCoverageLimit);

public sealed record InsuranceClaimReviewContext(
    long ClaimId,
    decimal RequestedAmount,
    decimal EligibleDamageAmount,
    decimal? PolicyCoverageAmount,
    decimal? PolicyDeductibleAmount,
    decimal ProviderCoverageLimit,
    decimal ApprovedAmount,
    string Reference,
    string Reason);

public sealed record InsuranceProviderAuditResponse(
    long Id,
    InsuranceProviderOperation Operation,
    InsuranceClaimStatus ResultStatus,
    decimal RequestedAmount,
    decimal ApprovedAmount,
    string ProviderReference,
    string RequestPayload,
    string ResponsePayload,
    Guid? PerformedByUserId,
    DateTime CreatedAtUtc);
