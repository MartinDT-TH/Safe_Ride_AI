using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Application.Features.Bookings;
using SafeRide.Domain.Enums;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
[Route("api/vehicles/{vehicleId:long}/insurance-policies")]
public sealed class VehicleInsurancePoliciesController : ControllerBase
{
    private readonly IVehicleInsurancePolicyService _service;
    private readonly IInsuranceDocumentService _documents;
    public VehicleInsurancePoliciesController(IVehicleInsurancePolicyService service, IInsuranceDocumentService documents)
    { _service = service; _documents = documents; }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VehicleInsurancePolicyResponse>>> Get(
        long vehicleId, CancellationToken cancellationToken)
        => Ok(await _service.GetAsync(GetUserId(), vehicleId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<VehicleInsurancePolicyResponse>> Create(
        long vehicleId, [FromBody] VehicleInsurancePolicyRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(GetUserId(), vehicleId, request, cancellationToken);
        return Created($"/api/vehicles/{vehicleId}/insurance-policies/{result.Id}", result);
    }

    [HttpPut("{policyId:long}")]
    public async Task<ActionResult<VehicleInsurancePolicyResponse>> Update(
        long vehicleId, long policyId, [FromBody] VehicleInsurancePolicyRequest request,
        CancellationToken cancellationToken)
        => Ok(await _service.UpdateAsync(
            GetUserId(), vehicleId, policyId, request, cancellationToken));

    [HttpDelete("{policyId:long}")]
    public async Task<IActionResult> Delete(long vehicleId, long policyId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(GetUserId(), vehicleId, policyId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{policyId:long}/documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_485_760)]
    public async Task<ActionResult<InsurancePolicyDocumentResponse>> UploadDocument(
        long vehicleId, long policyId, [FromForm] InsurancePolicyDocumentType documentType,
        [FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null) throw new BookingException("insurance.document_invalid", "Vui lòng tải lên chứng từ.", 400);
        await using var content = file.OpenReadStream();
        var result = await _documents.UploadPolicyDocumentAsync(GetUserId(), policyId, documentType,
            new InsuranceDocumentUpload(file.FileName, file.ContentType, file.Length, content), cancellationToken);
        return Created($"/api/vehicles/{vehicleId}/insurance-policies/{policyId}/documents/{result.Id}", result);
    }

    [HttpGet("{policyId:long}/documents")]
    public async Task<ActionResult<IReadOnlyList<InsurancePolicyDocumentResponse>>> GetDocuments(long vehicleId, long policyId, CancellationToken cancellationToken)
        => Ok(await _documents.ListPolicyDocumentsAsync(GetUserId(), policyId, false, cancellationToken));

    [HttpGet("{policyId:long}/documents/{documentId:long}/content")]
    public async Task<IActionResult> GetDocumentContent(long vehicleId, long policyId, long documentId, CancellationToken cancellationToken)
    {
        var document = await _documents.OpenPolicyDocumentAsync(GetUserId(), policyId, documentId, false, cancellationToken);
        return File(document.Content, document.ContentType, document.FileName);
    }

    private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException();
}
