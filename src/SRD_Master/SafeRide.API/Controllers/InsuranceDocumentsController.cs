using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Enums;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Staff,Admin")]
[Route("api/staff")]
public sealed class InsuranceDocumentsController : ControllerBase
{
    private readonly IInsuranceDocumentService _documents;
    public InsuranceDocumentsController(IInsuranceDocumentService documents) => _documents = documents;

    [HttpGet("vehicle-insurance-policies/{policyId:long}/documents")]
    public async Task<ActionResult<IReadOnlyList<InsurancePolicyDocumentResponse>>> ListPolicy(long policyId, CancellationToken cancellationToken)
        => Ok(await _documents.ListPolicyDocumentsAsync(GetUserId(), policyId, true, cancellationToken));

    [HttpGet("vehicle-insurance-policies/{policyId:long}/documents/{documentId:long}/content")]
    public async Task<IActionResult> PolicyContent(long policyId, long documentId, CancellationToken cancellationToken)
    {
        var document = await _documents.OpenPolicyDocumentAsync(GetUserId(), policyId, documentId, true, cancellationToken);
        return File(document.Content, document.ContentType, document.FileName);
    }

    [HttpPost("claims/{claimId:long}/insurance-documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_485_760)]
    public async Task<ActionResult<InsuranceClaimDocumentResponse>> UploadClaim(long claimId,
        [FromForm] InsuranceClaimDocumentType documentType, [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null) throw InvalidFile();
        await using var content = file.OpenReadStream();
        var result = await _documents.UploadClaimDocumentAsync(GetUserId(), claimId, documentType,
            new InsuranceDocumentUpload(file.FileName, file.ContentType, file.Length, content), cancellationToken);
        return Ok(result);
    }

    [HttpGet("claims/{claimId:long}/insurance-documents")]
    public async Task<ActionResult<IReadOnlyList<InsuranceClaimDocumentResponse>>> ListClaim(long claimId, CancellationToken cancellationToken)
        => Ok(await _documents.ListClaimDocumentsAsync(GetUserId(), claimId, cancellationToken));

    [HttpGet("claims/{claimId:long}/insurance-documents/{documentId:long}/content")]
    public async Task<IActionResult> ClaimContent(long claimId, long documentId, CancellationToken cancellationToken)
    {
        var document = await _documents.OpenClaimDocumentAsync(GetUserId(), claimId, documentId, cancellationToken);
        return File(document.Content, document.ContentType, document.FileName);
    }

    private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : throw new UnauthorizedAccessException();
    private static BookingException InvalidFile() => new("insurance.document_invalid", "Vui lòng tải lên chứng từ.", StatusCodes.Status400BadRequest);
}
