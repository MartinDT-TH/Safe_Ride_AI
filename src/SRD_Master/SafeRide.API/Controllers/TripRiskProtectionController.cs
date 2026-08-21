using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Enums;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize]
[Route("api/trips")]
public sealed class TripRiskProtectionController : ControllerBase
{
    private readonly IPreTripVehicleCheckService _preTripService;
    private readonly ITripStatusService _tripStatusService;
    private readonly IAccidentManagementService _accidentService;
    private readonly ISafetyReportService _safetyReportService;
    private readonly IPreTripVehicleCheckEvidenceStorage _preTripEvidenceStorage;
    private readonly ISafetyTerminationEvidenceStorage _safetyTerminationEvidenceStorage;
    private readonly IEvidenceFileValidator _evidenceFileValidator;
    private readonly ILogger<TripRiskProtectionController> _logger;

    public TripRiskProtectionController(
        IPreTripVehicleCheckService preTripService,
        ITripStatusService tripStatusService,
        IAccidentManagementService accidentService,
        ISafetyReportService safetyReportService,
        IPreTripVehicleCheckEvidenceStorage preTripEvidenceStorage,
        IEvidenceFileValidator evidenceFileValidator,
        ILogger<TripRiskProtectionController> logger)
        : this(
            preTripService, tripStatusService, accidentService, safetyReportService,
            preTripEvidenceStorage, new UnsupportedSafetyTerminationEvidenceStorage(),
            evidenceFileValidator, logger)
    {
    }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public TripRiskProtectionController(
        IPreTripVehicleCheckService preTripService,
        ITripStatusService tripStatusService,
        IAccidentManagementService accidentService,
        ISafetyReportService safetyReportService,
        IPreTripVehicleCheckEvidenceStorage preTripEvidenceStorage,
        ISafetyTerminationEvidenceStorage safetyTerminationEvidenceStorage,
        IEvidenceFileValidator evidenceFileValidator,
        ILogger<TripRiskProtectionController> logger)
    {
        _preTripService = preTripService;
        _tripStatusService = tripStatusService;
        _accidentService = accidentService;
        _safetyReportService = safetyReportService;
        _preTripEvidenceStorage = preTripEvidenceStorage;
        _safetyTerminationEvidenceStorage = safetyTerminationEvidenceStorage;
        _evidenceFileValidator = evidenceFileValidator;
        _logger = logger;
    }

    [HttpPost("{tripId:long}/vehicle-safety-checks")]
    [Authorize(Roles = "Driver")]
    [Consumes("application/json")]
    public async Task<ActionResult<PreTripVehicleCheckResponse>> CreateVehicleSafetyCheck(
        long tripId, [FromBody] PreTripVehicleCheckRequest request, CancellationToken cancellationToken)
    {
        var result = await _preTripService.CreateAsync(
            GetUserId(), tripId, request, evidence: null, cancellationToken);
        return Created($"/api/trips/{tripId}/vehicle-safety-checks/{result.Id}", result);
    }

    [HttpPost("{tripId:long}/vehicle-safety-checks")]
    [Authorize(Roles = "Driver")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_485_760)]
    public async Task<ActionResult<PreTripVehicleCheckResponse>> CreateVehicleSafetyCheckWithEvidence(
        long tripId,
        [FromForm] bool brakeResponsePassed,
        [FromForm] bool frontRearLightsPassed,
        [FromForm] bool turnSignalsPassed,
        [FromForm] bool visibleTiresPassed,
        [FromForm] bool dashboardWarningPassed,
        [FromForm] bool windshieldVisibilityPassed,
        [FromForm] bool noMajorVisibleIssue,
        [FromForm] VehicleFaultType? faultType,
        [FromForm] string? note,
        [FromForm] IFormFile evidence,
        CancellationToken cancellationToken)
    {
        var driverId = GetUserId();
        await _preTripService.EnsureCanCreateAsync(driverId, tripId, cancellationToken);
        await using var source = evidence.OpenReadStream();
        var validated = await ValidateEvidenceAsync(
            evidence, source, "pretrip", cancellationToken);
        await using var content = validated.Content;
        var stored = await _preTripEvidenceStorage.SaveAsync(
            tripId,
            validated.FileName,
            validated.ContentType,
            validated.FileSizeBytes,
            content,
            cancellationToken);
        try
        {
            var request = new PreTripVehicleCheckRequest(
                brakeResponsePassed,
                frontRearLightsPassed,
                turnSignalsPassed,
                visibleTiresPassed,
                dashboardWarningPassed,
                windshieldVisibilityPassed,
                noMajorVisibleIssue,
                faultType,
                note,
                EvidenceUrl: null);
            var result = await _preTripService.CreateAsync(
                driverId, tripId, request, stored, cancellationToken);
            return Created($"/api/trips/{tripId}/vehicle-safety-checks/{result.Id}", result);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(stored.StoragePublicId))
            {
                try
                {
                    await _preTripEvidenceStorage.DeleteAsync(
                        stored.StoragePublicId,
                        stored.ContentType,
                        CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    _logger.LogWarning(
                        cleanupException,
                        "Could not delete orphaned pre-trip evidence {PublicId} for trip {TripId}.",
                        stored.StoragePublicId,
                        tripId);
                }
            }
            throw;
        }
    }

    [HttpGet("{tripId:long}/vehicle-safety-checks")]
    public async Task<ActionResult<IReadOnlyList<PreTripVehicleCheckResponse>>> GetVehicleSafetyChecks(
        long tripId, CancellationToken cancellationToken)
        => Ok(await _preTripService.GetAsync(
            GetUserId(), User.IsInRole("Staff") || User.IsInRole("Admin"), tripId, cancellationToken));

    [HttpPost("{tripId:long}/safety-termination")]
    [Authorize(Roles = "Driver,Staff")]
    [Consumes("application/json")]
    public async Task<IActionResult> SafetyTerminate(
        long tripId, [FromBody] SafetyTerminationRequest request, CancellationToken cancellationToken)
    {
        await _tripStatusService.SafetyTerminateAsync(
            GetUserId(), User.IsInRole("Staff"), tripId, request.Reason, cancellationToken);
        return NoContent();
    }

    [HttpPost("{tripId:long}/safety-termination")]
    [Authorize(Roles = "Driver,Staff")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_485_760)]
    public async Task<IActionResult> SafetyTerminateWithEvidence(
        long tripId,
        [FromForm] string reason,
        [FromForm] IFormFile evidence,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var isStaff = User.IsInRole("Staff");
        await _tripStatusService.EnsureCanSafetyTerminateAsync(
            userId, isStaff, tripId, reason, cancellationToken);
        await using var source = evidence.OpenReadStream();
        var validated = await ValidateEvidenceAsync(
            evidence, source, "trip", cancellationToken);
        await using var content = validated.Content;
        var stored = await _safetyTerminationEvidenceStorage.SaveAsync(
            tripId, validated.FileName, validated.ContentType,
            validated.FileSizeBytes, content, cancellationToken);
        try
        {
            await _tripStatusService.SafetyTerminateAsync(
                userId, isStaff, tripId, reason, stored, cancellationToken);
            return NoContent();
        }
        catch
        {
            try
            {
                await _safetyTerminationEvidenceStorage.DeleteAsync(
                    stored.StoragePublicId, stored.ContentType, CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                _logger.LogWarning(
                    cleanupException,
                    "Could not delete orphaned safety termination evidence {PublicId} for trip {TripId}.",
                    stored.StoragePublicId,
                    tripId);
            }
            throw;
        }
    }

    [HttpPost("{tripId:long}/safety-reports")]
    [Authorize(Roles = "Driver")]
    public async Task<ActionResult<SafetyReportResponse>> CreateSafetyReport(
        long tripId, [FromBody] SafetyReportRequest request, CancellationToken cancellationToken)
        => Ok(await _safetyReportService.CreateAsync(GetUserId(), tripId, request, cancellationToken));

    [HttpPost("{tripId:long}/accidents")]
    [Authorize(Roles = "Customer,Driver,Staff,Admin")]
    public async Task<ActionResult<AccidentResponse>> CreateAccident(
        long tripId, [FromBody] CreateAccidentRequest request, CancellationToken cancellationToken)
    {
        var result = await _accidentService.CreateAsync(
            GetUserId(), User.IsInRole("Staff") || User.IsInRole("Admin"), tripId, request, cancellationToken);
        return Created($"/api/accidents/{result.Id}", result);
    }

    private Guid GetUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new UnauthorizedAccessException();

    private Task<ValidatedEvidenceFile> ValidateEvidenceAsync(
        IFormFile evidence,
        Stream source,
        string codePrefix,
        CancellationToken cancellationToken) =>
        _evidenceFileValidator.ValidateAsync(
            new EvidenceFileValidationRequest(
                evidence.FileName,
                evidence.ContentType,
                evidence.Length,
                source,
                AllowedEvidenceContentTypes,
                10_000_000,
                new EvidenceFileValidationErrorCodes(
                    $"{codePrefix}.evidence_invalid",
                    $"{codePrefix}.evidence_malware_detected",
                    $"{codePrefix}.evidence_scanner_unavailable")),
            cancellationToken);

    private static readonly string[] AllowedEvidenceContentTypes =
        ["image/jpeg", "image/png", "image/webp", "application/pdf"];

    private sealed class UnsupportedSafetyTerminationEvidenceStorage
        : ISafetyTerminationEvidenceStorage
    {
        public Task<StoredSafetyTerminationEvidence> SaveAsync(
            long tripId, string originalFileName, string contentType, long fileSizeBytes,
            Stream content, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Safety termination evidence storage is unavailable.");

        public Task DeleteAsync(string publicId, string contentType, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}

public sealed record SafetyTerminationRequest(string Reason);
