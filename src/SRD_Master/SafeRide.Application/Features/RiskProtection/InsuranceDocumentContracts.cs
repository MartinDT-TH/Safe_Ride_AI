using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.RiskProtection;

public sealed record InsuranceClaimDocumentResponse(
    long Id, InsuranceClaimDocumentType DocumentType, string OriginalFileName,
    string ContentType, long FileSizeBytes, DateTime UploadedAtUtc);
