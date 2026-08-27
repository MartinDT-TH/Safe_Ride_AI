using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Enums;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Staff")]
[Route("api/staff")]
public sealed class StaffRiskProtectionController : ControllerBase
{
    private readonly IAccidentManagementService _service;
    private readonly IVehicleInsurancePolicyService _insurancePolicies;
    private readonly IAccidentEvidenceStorage _evidenceStorage;
    private readonly IEvidenceFileValidator _evidenceFileValidator;
    private readonly ILogger<StaffRiskProtectionController> _logger;
    public StaffRiskProtectionController(
        IAccidentManagementService service,
        IVehicleInsurancePolicyService insurancePolicies,
        IAccidentEvidenceStorage evidenceStorage,
        IEvidenceFileValidator evidenceFileValidator,
        ILogger<StaffRiskProtectionController> logger)
    {
        _service = service;
        _insurancePolicies = insurancePolicies;
        _evidenceStorage = evidenceStorage;
        _evidenceFileValidator = evidenceFileValidator;
        _logger = logger;
    }

    [HttpPut("vehicle-insurance-policies/{policyId:long}/verification")]
    public async Task<ActionResult<VehicleInsurancePolicyResponse>> ReviewInsurancePolicy(
        long policyId, [FromBody] InsurancePolicyVerificationRequest request,
        CancellationToken cancellationToken)
        => Ok(await _insurancePolicies.ReviewAsync(
            GetUserId(), policyId, request.Status, cancellationToken));

    [HttpGet("accidents")]
    public async Task<ActionResult<IReadOnlyList<AccidentResponse>>> GetAccidents(
        [FromQuery] AccidentStatus? status,
        [FromQuery] AccidentCategory? category,
        [FromQuery] long? tripId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
        => Ok(await _service.GetStaffQueueAsync(
            new AccidentQueueFilter(status, category, tripId, fromUtc, toUtc, limit),
            cancellationToken));

    [HttpPut("accidents/{accidentId:long}/liability-assessment")]
    public async Task<ActionResult<ProtectionClaimResponse>> SaveAssessment(
        long accidentId, [FromBody] LiabilityAssessmentRequest request, CancellationToken cancellationToken)
        => Ok(await _service.SaveAssessmentAsync(GetUserId(), accidentId, request, false, cancellationToken));

    [HttpPost("accidents/{accidentId:long}/liability-assessment/confirm")]
    public async Task<ActionResult<ProtectionClaimResponse>> ConfirmAssessment(
        long accidentId, [FromBody] LiabilityAssessmentRequest request, CancellationToken cancellationToken)
        => Ok(await _service.SaveAssessmentAsync(GetUserId(), accidentId, request, true, cancellationToken));

    [HttpPost("claims/{claimId:long}/calculate")]
    public async Task<ActionResult<ProtectionClaimResponse>> CalculateClaim(
        long claimId, [FromBody] CalculateClaimRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CalculateClaimAsync(GetUserId(), claimId, request, cancellationToken));

    [HttpPost("claims/{claimId:long}/mock-insurance/approve")]
    public async Task<ActionResult<ProtectionClaimResponse>> ApproveMockInsurance(
        long claimId, [FromBody] InsuranceReviewRequest request, CancellationToken cancellationToken)
        => Ok(await _service.ReviewMockInsuranceAsync(
            GetUserId(), claimId, request, true, cancellationToken));

    [HttpPost("claims/{claimId:long}/mock-insurance/reject")]
    public async Task<ActionResult<ProtectionClaimResponse>> RejectMockInsurance(
        long claimId, [FromBody] InsuranceReviewRequest request, CancellationToken cancellationToken)
        => Ok(await _service.ReviewMockInsuranceAsync(
            GetUserId(), claimId, request, false, cancellationToken));

    [HttpGet("claims/{claimId:long}/mock-insurance/audits")]
    public async Task<ActionResult<IReadOnlyList<InsuranceProviderAuditResponse>>> GetMockInsuranceAudits(
        long claimId,
        CancellationToken cancellationToken)
        => Ok(await _service.GetInsuranceAuditsAsync(claimId, cancellationToken));

    [HttpPost("claims/{claimId:long}/mock-insurance/status")]
    public async Task<ActionResult<ProtectionClaimResponse>> RefreshMockInsuranceStatus(
        long claimId,
        [FromBody] InsuranceStatusRefreshRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.RefreshMockInsuranceStatusAsync(
            GetUserId(), claimId, request.RowVersion, cancellationToken));

    [HttpPost("claims/{claimId:long}/approve-funding")]
    [HttpPost("claims/{claimId:long}/retry-funding")]
    public async Task<ActionResult<ProtectionClaimResponse>> FundClaim(
        long claimId, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromBody] ClaimFundingRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.FundClaimAsync(
            GetUserId(), claimId, idempotencyKey, request.RowVersion, cancellationToken));

    [HttpPost("claims/{claimId:long}/recoveries")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_485_760)]
    public async Task<ActionResult<ProtectionClaimResponse>> RecordRecovery(
        long claimId,
        [FromForm] RecoverySourceType sourceType,
        [FromForm] string payerReference,
        [FromForm] decimal amount,
        [FromForm] string paymentReference,
        [FromForm] string rowVersion,
        [FromForm] IFormFile evidence,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await _service.EnsureCanRecordRecoveryEvidenceAsync(
            claimId, idempotencyKey, cancellationToken);
        var stored = await StoreTrustedEvidenceAsync(claimId, evidence, cancellationToken);
        try
        {
            return Ok(await _service.RecordRecoveryAsync(
                GetUserId(), claimId,
                new ClaimRecoveryRequest(
                    sourceType, payerReference, amount, paymentReference,
                    stored.Evidence, idempotencyKey, rowVersion),
                cancellationToken));
        }
        catch
        {
            await DeleteOrphanedEvidenceAsync(stored, claimId, CancellationToken.None);
            throw;
        }
    }

    [HttpPost("claims/{claimId:long}/write-offs")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_485_760)]
    public async Task<ActionResult<ProtectionClaimResponse>> WriteOffAdvance(
        long claimId,
        [FromForm] decimal amount,
        [FromForm] string reason,
        [FromForm] string rowVersion,
        [FromForm] IFormFile evidence,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        await _service.EnsureCanWriteOffEvidenceAsync(
            claimId, idempotencyKey, cancellationToken);
        var stored = await StoreTrustedEvidenceAsync(claimId, evidence, cancellationToken);
        try
        {
            return Ok(await _service.WriteOffAdvanceAsync(
                GetUserId(), claimId,
                new ClaimWriteOffRequest(amount, reason, stored.Evidence, idempotencyKey, rowVersion),
                cancellationToken));
        }
        catch
        {
            await DeleteOrphanedEvidenceAsync(stored, claimId, CancellationToken.None);
            throw;
        }
    }

    [HttpPost("claims/{claimId:long}/close")]
    public async Task<ActionResult<ProtectionClaimResponse>> CloseClaim(
        long claimId, [FromBody] CloseClaimRequest request, CancellationToken cancellationToken)
        => Ok(await _service.CloseClaimAsync(GetUserId(), claimId, request, cancellationToken));

    private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException();

    private async Task<StoredControllerEvidence> StoreTrustedEvidenceAsync(
        long claimId, IFormFile file, CancellationToken cancellationToken)
    {
        await using var source = file.OpenReadStream();
        var validated = await _evidenceFileValidator.ValidateAsync(
            new EvidenceFileValidationRequest(
                file.FileName,
                file.ContentType,
                file.Length,
                source,
                AllowedContentTypes,
                10_000_000,
                new EvidenceFileValidationErrorCodes(
                    "risk_protection.evidence_invalid",
                    "risk_protection.evidence_malware_detected",
                    "risk_protection.evidence_scanner_unavailable")),
            cancellationToken);
        await using var stream = validated.Content;
        var stored = await _evidenceStorage.SaveAsync(
            claimId, validated.FileName, validated.ContentType,
            validated.FileSizeBytes, stream, cancellationToken);
        if (string.IsNullOrWhiteSpace(stored.PublicId))
            throw InvalidEvidence("Kho lưu trữ không trả về định danh bằng chứng tin cậy.");
        return new StoredControllerEvidence(
            new TrustedClaimEvidence(
                stored.FileUrl, stored.PublicId, validated.FileName, validated.ContentType,
                stored.FileSizeBytes ?? validated.FileSizeBytes),
            stored.PublicId,
            validated.ContentType);
    }

    private async Task DeleteOrphanedEvidenceAsync(
        StoredControllerEvidence stored, long claimId, CancellationToken cancellationToken)
    {
        try
        {
            await _evidenceStorage.DeleteAsync(stored.PublicId, stored.ContentType, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Could not delete orphaned claim evidence {PublicId} for claim {ClaimId}.",
                stored.PublicId, claimId);
        }
    }

    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp", "application/pdf"];

    private static BookingException InvalidEvidence(string detail) => new(
        "risk_protection.evidence_invalid", detail, StatusCodes.Status400BadRequest);

    private sealed record StoredControllerEvidence(
        TrustedClaimEvidence Evidence, string PublicId, string ContentType);
}

public sealed record InsurancePolicyVerificationRequest(InsuranceVerificationStatus Status);
public sealed record InsuranceStatusRefreshRequest(string RowVersion);
