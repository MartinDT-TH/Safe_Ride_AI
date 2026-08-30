using SafeRide.Application.Features.RiskProtection;
using SafeRide.Application.Common.Realtime;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Interfaces;

public interface ITripCommissionCalculator
{
    CommissionCalculationResult Calculate(CommissionCalculationInput input);
    ComponentAwareCommissionCalculationResult CalculateComponentAware(
        ComponentAwareCommissionCalculationInput input);
}

public interface IClaimSettlementCalculator
{
    DriverLiabilityCalculationResult CalculateDriverLiability(DriverLiabilityCalculationInput input);
    ClaimLiabilityCalculationResult CalculateLiabilities(ClaimLiabilityCalculationInput input);
}

public interface IRiskProtectionPolicyProvider
{
    Task<RiskProtectionPolicyVersion> GetEffectivePolicyAsync(DateTime utcNow, CancellationToken cancellationToken);
    Task<RiskProtectionPolicyResponse?> GetCurrentAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<RiskProtectionPolicyResponse>> ListAsync(CancellationToken cancellationToken);
    Task<RiskProtectionPolicyResponse> CreateAsync(Guid adminUserId, CreateRiskProtectionPolicyRequest request, CancellationToken cancellationToken);
}

public interface IPreTripVehicleCheckService
{
    Task EnsureCanCreateAsync(Guid driverId, long tripId, CancellationToken cancellationToken);
    Task<PreTripVehicleCheckResponse> CreateAsync(
        Guid driverId,
        long tripId,
        PreTripVehicleCheckRequest request,
        StoredPreTripVehicleCheckEvidence? evidence,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<PreTripVehicleCheckResponse>> GetAsync(Guid userId, bool isManagement, long tripId, CancellationToken cancellationToken);
    Task EnsureCanStartAndActivateCoverageAsync(Guid driverId, Trip trip, DateTime startedAtUtc, CancellationToken cancellationToken);
}

public interface ITripFinancialSettlementService
{
    Task<TripFinancialSettlement> GetOrCreateAsync(Trip trip, bool safetyTerminated, CancellationToken cancellationToken);
    Task SettleQrDriverEarningAsync(Trip trip, string? providerReference, CancellationToken cancellationToken);
    Task ApplyCashWalletAdjustmentAsync(Trip trip, CancellationToken cancellationToken);
    Task CreateContributionForCompletedTripAsync(Trip trip, CancellationToken cancellationToken);
}

public interface ISafetyPaymentReconciliationService
{
    Task<SafetyPaymentReconciliation> ReconcileAsync(Trip trip, CancellationToken cancellationToken);
    Task<IReadOnlyList<ManualRefundQueueItemResponse>> ListRefundsAsync(
        ManualRefundStatus? status,
        CancellationToken cancellationToken);
    Task<SafetyPaymentReconciliationResponse> ConfirmManualRefundAsync(
        Guid staffUserId,
        long refundId,
        ManualRefundConfirmationRequest request,
        CancellationToken cancellationToken);
}

public interface ISafetyTerminationEvidenceStorage
{
    Task<StoredSafetyTerminationEvidence> SaveAsync(
        long tripId,
        string originalFileName,
        string contentType,
        long fileSizeBytes,
        Stream content,
        CancellationToken cancellationToken);
    Task DeleteAsync(string publicId, string contentType, CancellationToken cancellationToken);
}

public interface IVehicleInsurancePolicyService
{
    Task<IReadOnlyList<VehicleInsurancePolicyResponse>> GetAsync(Guid ownerUserId, long vehicleId, CancellationToken cancellationToken);
    Task<VehicleInsurancePolicyResponse> CreateAsync(Guid ownerUserId, long vehicleId, VehicleInsurancePolicyRequest request, CancellationToken cancellationToken);
    Task<VehicleInsurancePolicyResponse> UpdateAsync(Guid ownerUserId, long vehicleId, long policyId, VehicleInsurancePolicyRequest request, CancellationToken cancellationToken);
    Task<VehicleInsurancePolicyResponse> ReviewAsync(Guid staffUserId, long policyId, InsuranceVerificationStatus status, CancellationToken cancellationToken);
    Task DeleteAsync(Guid ownerUserId, long vehicleId, long policyId, CancellationToken cancellationToken);
}

public interface ISafetyReportService
{
    Task<SafetyReportResponse> CreateAsync(Guid driverId, long tripId, SafetyReportRequest request, CancellationToken cancellationToken);
}

public interface IAccidentManagementService
{
    Task<AccidentResponse> CreateAsync(Guid userId, bool isManagement, long tripId, CreateAccidentRequest request, CancellationToken cancellationToken);
    Task<AccidentResponse> GetAsync(Guid userId, bool isManagement, long accidentId, CancellationToken cancellationToken);
    Task EnsureCanUploadEvidenceAsync(Guid userId, bool isManagement, long accidentId, CancellationToken cancellationToken);
    Task<AccidentEvidenceResponse> AddEvidenceAsync(Guid userId, bool isManagement, long accidentId, AddAccidentEvidenceRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<AccidentResponse>> GetStaffQueueAsync(AccidentQueueFilter filter, CancellationToken cancellationToken);
    Task<ProtectionClaimResponse> SaveAssessmentAsync(Guid staffUserId, long accidentId, LiabilityAssessmentRequest request, bool confirm, CancellationToken cancellationToken);
    Task<ProtectionClaimResponse> CalculateClaimAsync(Guid staffUserId, long claimId, CalculateClaimRequest request, CancellationToken cancellationToken);
    Task<ProtectionClaimResponse> ReviewMockInsuranceAsync(
        Guid staffUserId,
        long claimId,
        InsuranceReviewRequest request,
        bool approve,
        CancellationToken cancellationToken);
    Task<ProtectionClaimResponse> RefreshMockInsuranceStatusAsync(
        Guid staffUserId,
        long claimId,
        string rowVersion,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<InsuranceProviderAuditResponse>> GetInsuranceAuditsAsync(
        long claimId,
        CancellationToken cancellationToken);
    Task<ProtectionClaimResponse> FundClaimAsync(Guid staffUserId, long claimId, string idempotencyKey, string rowVersion, CancellationToken cancellationToken);
    Task<ProtectionClaimResponse> RecordRecoveryAsync(Guid staffUserId, long claimId, ClaimRecoveryRequest request, CancellationToken cancellationToken);
    Task EnsureCanRecordRecoveryEvidenceAsync(
        long claimId,
        string idempotencyKey,
        CancellationToken cancellationToken) => Task.CompletedTask;
    Task<ProtectionClaimResponse> WriteOffAdvanceAsync(Guid staffUserId, long claimId, ClaimWriteOffRequest request, CancellationToken cancellationToken);
    Task EnsureCanWriteOffEvidenceAsync(
        long claimId,
        string idempotencyKey,
        CancellationToken cancellationToken) => Task.CompletedTask;
    Task<ProtectionClaimResponse> CloseClaimAsync(Guid staffUserId, long claimId, CloseClaimRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<DriverLiabilityResponse>> GetDriverLiabilitiesAsync(Guid driverId, CancellationToken cancellationToken);
    Task DisputeLiabilityAsync(Guid userId, long accidentId, LiabilityDisputeRequest request, CancellationToken cancellationToken);
}

public interface IFileSafetyScanner
{
    Task<FileSafetyScanResult> ScanAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken);
}

public enum FileSafetyScanStatus
{
    Clean,
    ThreatDetected,
    ScannerUnavailable,
    DevelopmentBypass
}

public sealed record FileSafetyScanResult(
    FileSafetyScanStatus Status,
    string? ThreatName = null);

public interface IRiskFundLedgerService
{
    Task<RiskFundDashboardResponse> GetDashboardAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<RiskFundTransactionResponse>> GetTransactionsAsync(
        RiskFundTransactionType? type,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken);
    Task ExportTransactionsAsync(
        RiskFundTransactionType? type,
        DateTime? fromUtc,
        DateTime? toUtc,
        Stream output,
        CancellationToken cancellationToken);
    Task<RiskFundMutationResponse> ApplyOpeningBalanceAsync(
        Guid adminUserId,
        RiskFundMutationRequest request,
        CancellationToken cancellationToken);
    Task<RiskFundMutationResponse> ApplyAdjustmentAsync(
        Guid adminUserId,
        RiskFundMutationRequest request,
        CancellationToken cancellationToken);
}

public interface IInsuranceProvider
{
    Task<InsuranceCalculationResult> CalculateClaimAsync(
        InsuranceClaimCalculationContext context,
        CancellationToken cancellationToken);
    Task<InsuranceSubmissionResult> SubmitClaimAsync(
        InsuranceClaimSubmissionContext context,
        CancellationToken cancellationToken);
    Task<InsuranceSubmissionResult> GetClaimStatusAsync(
        InsuranceClaimStatusContext context,
        CancellationToken cancellationToken);
    Task<InsuranceSubmissionResult> ReviewClaimAsync(
        InsuranceClaimReviewContext context,
        bool approve,
        CancellationToken cancellationToken);
}

public interface IAccidentEvidenceStorage
{
    Task<StoredAccidentEvidenceFile> SaveAsync(
        long accidentId,
        string originalFileName,
        string contentType,
        long fileSizeBytes,
        Stream content,
        CancellationToken cancellationToken);

    Task DeleteAsync(string publicId, string contentType, CancellationToken cancellationToken);
}

public interface IInsuranceDocumentService
{
    Task<InsurancePolicyDocumentResponse> UploadPolicyDocumentAsync(Guid userId, long policyId, InsurancePolicyDocumentType type, InsuranceDocumentUpload upload, CancellationToken cancellationToken);
    Task<IReadOnlyList<InsurancePolicyDocumentResponse>> ListPolicyDocumentsAsync(Guid userId, long policyId, bool isStaff, CancellationToken cancellationToken);
    Task<InsuranceDocumentDownload> OpenPolicyDocumentAsync(Guid userId, long policyId, long documentId, bool isStaff, CancellationToken cancellationToken);
    Task<InsuranceClaimDocumentResponse> UploadClaimDocumentAsync(Guid staffUserId, long claimId, InsuranceClaimDocumentType type, InsuranceDocumentUpload upload, CancellationToken cancellationToken);
    Task<IReadOnlyList<InsuranceClaimDocumentResponse>> ListClaimDocumentsAsync(Guid staffUserId, long claimId, CancellationToken cancellationToken);
    Task<InsuranceDocumentDownload> OpenClaimDocumentAsync(Guid staffUserId, long claimId, long documentId, CancellationToken cancellationToken);
}

public sealed record InsuranceDocumentUpload(string FileName, string ContentType, long FileSizeBytes, Stream Content);
public sealed record InsuranceDocumentDownload(Stream Content, string FileName, string ContentType, long FileSizeBytes);

public interface IPrivateInsuranceDocumentStorage
{
    Task<StoredPrivateInsuranceDocument> SaveAsync(
        string aggregateType, long aggregateId, string fileName, string contentType,
        Stream content, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string objectKey, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}

public sealed record StoredPrivateInsuranceDocument(string ObjectKey, long FileSizeBytes);

public interface IPreTripVehicleCheckEvidenceStorage
{
    Task<StoredPreTripVehicleCheckEvidence> SaveAsync(
        long tripId,
        string originalFileName,
        string contentType,
        long fileSizeBytes,
        Stream content,
        CancellationToken cancellationToken);

    Task DeleteAsync(string publicId, string contentType, CancellationToken cancellationToken);
}

public interface IAccidentRealtimeService
{
    Task PublishAccidentCreatedAsync(
        AccidentCreatedEvent notification,
        CancellationToken cancellationToken = default);
}

public sealed record StoredAccidentEvidenceFile(
    string FileUrl,
    string? PublicId,
    long? FileSizeBytes);
