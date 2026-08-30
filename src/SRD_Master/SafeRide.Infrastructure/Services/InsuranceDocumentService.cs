using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class InsuranceDocumentService : IInsuranceDocumentService
{
    private static readonly string[] AllowedContentTypes = ["application/pdf", "image/jpeg", "image/png"];
    private readonly ApplicationDbContext _db;
    private readonly IEvidenceFileValidator _validator;
    private readonly IPrivateInsuranceDocumentStorage _storage;
    private readonly IDateTimeProvider _clock;
    private readonly EvidenceFileSafetyOptions _safetyOptions;

    public InsuranceDocumentService(ApplicationDbContext db, IEvidenceFileValidator validator,
        IPrivateInsuranceDocumentStorage storage, IDateTimeProvider clock,
        IOptions<EvidenceFileSafetyOptions> safetyOptions)
    { _db = db; _validator = validator; _storage = storage; _clock = clock; _safetyOptions = safetyOptions.Value; }

    public async Task<InsurancePolicyDocumentResponse> UploadPolicyDocumentAsync(Guid userId, long policyId,
        InsurancePolicyDocumentType type, InsuranceDocumentUpload upload, CancellationToken cancellationToken)
    {
        var policy = await _db.VehicleInsurancePolicies.Include(x => x.Vehicle)
            .SingleOrDefaultAsync(x => x.Id == policyId && !x.IsDeleted && x.Vehicle.OwnerUserId == userId, cancellationToken)
            ?? throw NotFound();
        var validated = await ValidateAsync(upload, cancellationToken);
        var key = await _storage.SaveAsync("policy", policyId, validated.FileName, validated.ContentType, validated.Content, cancellationToken);
        try
        {
            var document = new InsurancePolicyDocument { VehicleInsurancePolicyId = policy.Id, DocumentType = type,
                StorageObjectKey = key.ObjectKey, OriginalFileName = validated.FileName, ContentType = validated.ContentType,
                FileSizeBytes = key.FileSizeBytes, Sha256Hash = Hash(validated.Content), UploadedByUserId = userId, UploadedAtUtc = _clock.UtcNow };
            _db.InsurancePolicyDocuments.Add(document);
            await _db.SaveChangesAsync(cancellationToken);
            return Map(document);
        }
        catch { await _storage.DeleteAsync(key.ObjectKey, CancellationToken.None); throw; }
        finally { await validated.Content.DisposeAsync(); }
    }

    public async Task<IReadOnlyList<InsurancePolicyDocumentResponse>> ListPolicyDocumentsAsync(Guid userId, long policyId, bool isStaff, CancellationToken cancellationToken)
    {
        var policy = await _db.VehicleInsurancePolicies.Include(x => x.Vehicle)
            .SingleOrDefaultAsync(x => x.Id == policyId && !x.IsDeleted && (isStaff || x.Vehicle.OwnerUserId == userId), cancellationToken)
            ?? throw NotFound();
        return await _db.InsurancePolicyDocuments.AsNoTracking().Where(x => x.VehicleInsurancePolicyId == policy.Id)
            .OrderByDescending(x => x.UploadedAtUtc).Select(x => Map(x)).ToListAsync(cancellationToken);
    }

    public async Task<InsuranceDocumentDownload> OpenPolicyDocumentAsync(Guid userId, long policyId, long documentId, bool isStaff, CancellationToken cancellationToken)
    {
        var document = await _db.InsurancePolicyDocuments.AsNoTracking().Include(x => x.VehicleInsurancePolicy).ThenInclude(x => x.Vehicle)
            .SingleOrDefaultAsync(x => x.Id == documentId && x.VehicleInsurancePolicyId == policyId && !x.VehicleInsurancePolicy.IsDeleted
                && (isStaff || x.VehicleInsurancePolicy.Vehicle.OwnerUserId == userId), cancellationToken) ?? throw NotFound();
        return await OpenAsync(document.StorageObjectKey, document.OriginalFileName, document.ContentType, document.FileSizeBytes, cancellationToken);
    }

    public async Task<InsuranceClaimDocumentResponse> UploadClaimDocumentAsync(Guid staffUserId, long claimId,
        InsuranceClaimDocumentType type, InsuranceDocumentUpload upload, CancellationToken cancellationToken)
    {
        var claim = await _db.ProtectionClaims.SingleOrDefaultAsync(x => x.Id == claimId, cancellationToken) ?? throw NotFoundClaim();
        EnsureTypeAllowed(claim.InsuranceStatus, type);
        var validated = await ValidateAsync(upload, cancellationToken);
        var key = await _storage.SaveAsync("claim", claimId, validated.FileName, validated.ContentType, validated.Content, cancellationToken);
        try
        {
            var document = new InsuranceClaimDocument { ProtectionClaimId = claim.Id, DocumentType = type,
                StorageObjectKey = key.ObjectKey, OriginalFileName = validated.FileName, ContentType = validated.ContentType,
                FileSizeBytes = key.FileSizeBytes, Sha256Hash = Hash(validated.Content), UploadedByUserId = staffUserId, UploadedAtUtc = _clock.UtcNow };
            _db.InsuranceClaimDocuments.Add(document);
            await _db.SaveChangesAsync(cancellationToken);
            return Map(document);
        }
        catch { await _storage.DeleteAsync(key.ObjectKey, CancellationToken.None); throw; }
        finally { await validated.Content.DisposeAsync(); }
    }

    public async Task<IReadOnlyList<InsuranceClaimDocumentResponse>> ListClaimDocumentsAsync(Guid staffUserId, long claimId, CancellationToken cancellationToken)
    {
        if (!await _db.ProtectionClaims.AnyAsync(x => x.Id == claimId, cancellationToken)) throw NotFoundClaim();
        return await _db.InsuranceClaimDocuments.AsNoTracking().Where(x => x.ProtectionClaimId == claimId)
            .OrderByDescending(x => x.UploadedAtUtc).Select(x => Map(x)).ToListAsync(cancellationToken);
    }

    public async Task<InsuranceDocumentDownload> OpenClaimDocumentAsync(Guid staffUserId, long claimId, long documentId, CancellationToken cancellationToken)
    {
        var document = await _db.InsuranceClaimDocuments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == documentId && x.ProtectionClaimId == claimId, cancellationToken) ?? throw NotFoundClaim();
        return await OpenAsync(document.StorageObjectKey, document.OriginalFileName, document.ContentType, document.FileSizeBytes, cancellationToken);
    }

    private async Task<ValidatedEvidenceFile> ValidateAsync(InsuranceDocumentUpload upload, CancellationToken cancellationToken)
    {
        if (string.Equals(_safetyOptions.ScannerType, "PublicDemo", StringComparison.OrdinalIgnoreCase))
            throw new BookingException("insurance.document_scanner_unavailable", "PublicDemo chỉ hỗ trợ chứng từ mô phỏng không nhạy cảm.", StatusCodes.Status503ServiceUnavailable);
        return await _validator.ValidateAsync(new EvidenceFileValidationRequest(upload.FileName, upload.ContentType, upload.FileSizeBytes,
            upload.Content, AllowedContentTypes, 10_000_000,
            new EvidenceFileValidationErrorCodes("insurance.document_invalid", "insurance.document_malware_detected", "insurance.document_scanner_unavailable")), cancellationToken);
    }

    private async Task<InsuranceDocumentDownload> OpenAsync(string key, string fileName, string contentType, long size, CancellationToken cancellationToken)
    {
        try { return new InsuranceDocumentDownload(await _storage.OpenReadAsync(key, cancellationToken), fileName, contentType, size); }
        catch (FileNotFoundException) { throw NotFound(); }
    }

    private static void EnsureTypeAllowed(InsuranceClaimStatus status, InsuranceClaimDocumentType type)
    {
        var allowed = type switch
        {
            InsuranceClaimDocumentType.INSURER_APPROVAL or InsuranceClaimDocumentType.INSURER_PARTIAL_APPROVAL => status == InsuranceClaimStatus.APPROVED,
            InsuranceClaimDocumentType.INSURER_REJECTION => status == InsuranceClaimStatus.REJECTED,
            _ => true
        };
        if (!allowed) throw new BookingException("insurance.document_type_invalid", "Loại chứng từ không phù hợp trạng thái bảo hiểm hiện tại.", StatusCodes.Status400BadRequest);
    }
    private static string Hash(Stream content) { if (content.CanSeek) content.Position = 0; return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(); }
    private static InsurancePolicyDocumentResponse Map(InsurancePolicyDocument x) => new(x.Id, x.DocumentType, x.OriginalFileName, x.ContentType, x.FileSizeBytes, x.UploadedAtUtc);
    private static InsuranceClaimDocumentResponse Map(InsuranceClaimDocument x) => new(x.Id, x.DocumentType, x.OriginalFileName, x.ContentType, x.FileSizeBytes, x.UploadedAtUtc);
    private static BookingException NotFound() => new("insurance.document_not_found", "Không tìm thấy chứng từ bảo hiểm.", StatusCodes.Status404NotFound);
    private static BookingException NotFoundClaim() => new("insurance.claim_not_found", "Không tìm thấy hồ sơ bảo hiểm.", StatusCodes.Status404NotFound);
}
