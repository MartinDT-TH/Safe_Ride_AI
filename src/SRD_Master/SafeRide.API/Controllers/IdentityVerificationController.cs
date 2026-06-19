using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.IdentityVerification.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize]
[Route("api/identity-verification")]
public sealed class IdentityVerificationController : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "application/pdf"
    };

    private readonly ApplicationDbContext _dbContext;
    private readonly IIdentityDocumentStorage _documentStorage;

    public IdentityVerificationController(
        ApplicationDbContext dbContext,
        IIdentityDocumentStorage documentStorage)
    {
        _dbContext = dbContext;
        _documentStorage = documentStorage;
    }

    [HttpGet("documents")]
    public async Task<ActionResult<IReadOnlyList<IdentityVerificationDocumentResponse>>> GetDocuments(
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var driverId))
        {
            return Unauthorized();
        }

        var documents = await _dbContext.DriverKycs
            .AsNoTracking()
            .Where(x => x.DriverId == driverId)
            .OrderBy(x => x.DocumentType)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => ToResponse(x, null))
            .ToListAsync(cancellationToken);

        return Ok(documents);
    }

    [HttpPost("documents/{documentType}")]
    [RequestSizeLimit(MaxFileSizeBytes * 3)]
    public async Task<ActionResult<IdentityVerificationDocumentResponse>> UploadDocument(
        [FromRoute] KycDocumentType documentType,
        [FromForm] UploadIdentityDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var driverId))
        {
            return Unauthorized();
        }

        var validationError = ValidateRequiredFiles(documentType, request);
        if (validationError != null)
        {
            return validationError;
        }

        var metadataError = ValidateRequiredMetadata(documentType, request);
        if (metadataError != null)
        {
            return metadataError;
        }

        var scanRequest = new ScanIdentityDocumentRequest
        {
            DocumentType = documentType,
            FrontImage = request.FrontImage,
            BackImage = request.BackImage,
            File = request.File
        };
        var filesResult = await ReadFiles(scanRequest, cancellationToken);
        if (filesResult.Result != null)
        {
            return filesResult.Result;
        }

        var files = filesResult.Value!;
        var kyc = await _dbContext.DriverKycs.FirstOrDefaultAsync(
            x => x.DriverId == driverId && x.DocumentType == documentType,
            cancellationToken);

        if (kyc == null)
        {
            kyc = new DriverKyc
            {
                DriverId = driverId,
                DocumentType = documentType,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.DriverKycs.Add(kyc);
        }

        foreach (var file in files)
        {
            await using var stream = new MemoryStream(file.Content);
            var storedFile = await _documentStorage.SaveAsync(
                driverId,
                documentType,
                file.Slot,
                file.FileName,
                file.ContentType,
                stream,
                cancellationToken);

            ApplyStoredFile(kyc, file.Slot, storedFile.Url);
        }

        kyc.DocumentNumber = NormalizeOptional(request.DocumentNumber) ?? kyc.DocumentNumber;
        kyc.LicenseClass = request.LicenseClass ?? kyc.LicenseClass;
        kyc.IssueDate = request.IssueDate ?? kyc.IssueDate;
        kyc.ExpiryDate = request.ExpiryDate ?? kyc.ExpiryDate;
        kyc.KycStatus = KycStatus.Pending;
        kyc.VerifiedAt = null;
        kyc.RejectionReason = null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(kyc, null));
    }

    private async Task<ActionResult<IReadOnlyList<IdentityUploadInputFile>>> ReadFiles(
        ScanIdentityDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var files = new List<IdentityUploadInputFile>();

        foreach (var (slot, formFile) in EnumerateFiles(request))
        {
            var validationError = ValidateFile(formFile);
            if (validationError != null)
            {
                return validationError;
            }

            await using var stream = formFile.OpenReadStream();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            files.Add(new IdentityUploadInputFile(
                slot,
                formFile.FileName,
                formFile.ContentType,
                memory.ToArray()));
        }

        if (files.Count == 0)
        {
            return BadRequest(new
            {
                code = "identity_verification.file_required",
                message = "Vui lòng tải lên ít nhất một file giấy tờ."
            });
        }

        return files;
    }

    private static IEnumerable<(string Slot, IFormFile File)> EnumerateFiles(
        ScanIdentityDocumentRequest request)
    {
        if (request.FrontImage != null)
        {
            yield return ("front", request.FrontImage);
        }

        if (request.BackImage != null)
        {
            yield return ("back", request.BackImage);
        }

        if (request.File != null)
        {
            yield return ("file", request.File);
        }
    }

    private ActionResult? ValidateRequiredFiles(
        KycDocumentType documentType,
        UploadIdentityDocumentRequest request)
    {
        return documentType switch
        {
            KycDocumentType.ID_CARD when request.FrontImage == null || request.BackImage == null =>
                BadRequest(new
                {
                    code = "identity_verification.id_card_images_required",
                    message = "CCCD cần ảnh mặt trước và mặt sau."
                }),
            KycDocumentType.DRIVING_LICENSE when request.FrontImage == null || request.BackImage == null =>
                BadRequest(new
                {
                    code = "identity_verification.driving_license_images_required",
                    message = "GPLX cần ảnh mặt trước và mặt sau."
                }),
            KycDocumentType.CRIMINAL_RECORD when request.File == null =>
                BadRequest(new
                {
                    code = "identity_verification.criminal_record_file_required",
                    message = "Lý lịch tư pháp cần file ảnh hoặc PDF."
                }),
            _ => null
        };
    }

    private ActionResult? ValidateRequiredMetadata(
        KycDocumentType documentType,
        UploadIdentityDocumentRequest request)
    {
        return documentType switch
        {
            KycDocumentType.DRIVING_LICENSE when NormalizeOptional(request.DocumentNumber) == null =>
                BadRequest(new
                {
                    code = "identity_verification.driving_license_number_required",
                    message = "Vui lòng nhập số giấy phép lái xe."
                }),
            KycDocumentType.DRIVING_LICENSE when request.LicenseClass == null =>
                BadRequest(new
                {
                    code = "identity_verification.driving_license_class_required",
                    message = "Vui lòng chọn hạng giấy phép lái xe."
                }),
            _ => null
        };
    }

    private ActionResult? ValidateFile(IFormFile file)
    {
        if (file.Length <= 0)
        {
            return BadRequest(new
            {
                code = "identity_verification.empty_file",
                message = "File tải lên đang rỗng."
            });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new
            {
                code = "identity_verification.file_too_large",
                message = "Mỗi file giấy tờ chỉ được tối đa 10MB."
            });
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return BadRequest(new
            {
                code = "identity_verification.unsupported_file_type",
                message = "Chỉ hỗ trợ JPG, PNG hoặc PDF."
            });
        }

        return null;
    }

    private static void ApplyStoredFile(DriverKyc kyc, string slot, string url)
    {
        switch (slot)
        {
            case "front":
                kyc.FrontImageUrl = url;
                break;
            case "back":
                kyc.BackImageUrl = url;
                break;
            default:
                kyc.FileUrl = url;
                break;
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        return Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static IdentityVerificationDocumentResponse ToResponse(
        DriverKyc document,
        IdentityOcrResult? ocrResult)
    {
        return new IdentityVerificationDocumentResponse(
            document.Id,
            document.DocumentType,
            document.DocumentNumber,
            document.LicenseClass,
            document.FrontImageUrl,
            document.BackImageUrl,
            document.FileUrl,
            document.IssueDate,
            document.ExpiryDate,
            document.KycStatus,
            document.CreatedAt,
            document.VerifiedAt,
            document.RejectionReason,
            ocrResult);
    }
}

public sealed class ScanIdentityDocumentRequest
{
    public KycDocumentType DocumentType { get; set; }
    public IFormFile? FrontImage { get; set; }
    public IFormFile? BackImage { get; set; }
    public IFormFile? File { get; set; }
}

internal sealed record IdentityUploadInputFile(
    string Slot,
    string FileName,
    string ContentType,
    byte[] Content);

public sealed class UploadIdentityDocumentRequest
{
    public IFormFile? FrontImage { get; set; }
    public IFormFile? BackImage { get; set; }
    public IFormFile? File { get; set; }
    public string? DocumentNumber { get; set; }
    public LicenseClass? LicenseClass { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
}
