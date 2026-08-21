using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Enums;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Customer,Driver,Staff,Admin")]
[Route("api/accidents")]
public sealed class AccidentsController : ControllerBase
{
    private readonly IAccidentManagementService _service;
    private readonly IAccidentEvidenceStorage _evidenceStorage;
    private readonly IEvidenceFileValidator _evidenceFileValidator;
    private readonly ILogger<AccidentsController> _logger;
    public AccidentsController(
        IAccidentManagementService service,
        IAccidentEvidenceStorage evidenceStorage,
        IEvidenceFileValidator evidenceFileValidator,
        ILogger<AccidentsController> logger)
    {
        _service = service;
        _evidenceStorage = evidenceStorage;
        _evidenceFileValidator = evidenceFileValidator;
        _logger = logger;
    }

    [HttpGet("{accidentId:long}")]
    public async Task<ActionResult<AccidentResponse>> Get(long accidentId, CancellationToken cancellationToken)
        => Ok(await _service.GetAsync(GetUserId(), IsManagement(), accidentId, cancellationToken));

    [HttpPost("{accidentId:long}/evidence")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_485_760)]
    public async Task<IActionResult> UploadEvidence(
        long accidentId,
        [FromForm] IFormFile file,
        [FromForm] AccidentEvidenceType evidenceType,
        [FromForm] string? description,
        [FromForm] DateTime? capturedAtUtc,
        [FromForm] decimal? latitude,
        [FromForm] decimal? longitude,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var isManagement = IsManagement();
        await _service.EnsureCanUploadEvidenceAsync(
            userId, isManagement, accidentId, cancellationToken);
        if (!Enum.IsDefined(evidenceType))
            throw InvalidEvidence("Loại bằng chứng không hợp lệ.");

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
                    "accident.evidence_invalid",
                    "accident.evidence_malware_rejected",
                    "accident.evidence_scanner_unavailable")),
            cancellationToken);
        await using var stream = validated.Content;
        var stored = await _evidenceStorage.SaveAsync(
            accidentId, validated.FileName, validated.ContentType,
            stream, cancellationToken);
        try
        {
            var evidence = await _service.AddEvidenceAsync(
                userId, isManagement, accidentId,
                new AddAccidentEvidenceRequest(
                    evidenceType, stored.FileUrl, validated.FileName, validated.ContentType,
                    stored.PublicId, stored.FileSizeBytes ?? validated.FileSizeBytes, capturedAtUtc,
                    latitude, longitude, description),
                cancellationToken);
            return Created($"/api/accidents/{accidentId}", evidence);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(stored.PublicId))
            {
                try
                {
                    await _evidenceStorage.DeleteAsync(
                        stored.PublicId, validated.ContentType, CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    _logger.LogWarning(cleanupException,
                        "Could not delete orphaned evidence {PublicId} for accident {AccidentId}.",
                        stored.PublicId, accidentId);
                }
            }
            throw;
        }
    }

    [HttpPost("{accidentId:long}/disputes")]
    [Authorize(Roles = "Customer,Driver")]
    public async Task<IActionResult> Dispute(
        long accidentId, [FromBody] LiabilityDisputeRequest request, CancellationToken cancellationToken)
    {
        await _service.DisputeLiabilityAsync(GetUserId(), accidentId, request, cancellationToken);
        return NoContent();
    }

    private bool IsManagement() => User.IsInRole("Staff") || User.IsInRole("Admin");
    private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException();

    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp", "application/pdf"];

    private static BookingException InvalidEvidence(string detail) => new(
        "accident.evidence_invalid",
        detail,
        StatusCodes.Status400BadRequest);
}
