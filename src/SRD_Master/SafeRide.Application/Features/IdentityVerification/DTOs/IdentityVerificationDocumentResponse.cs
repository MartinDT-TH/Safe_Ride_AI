using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.IdentityVerification.DTOs;

public sealed record IdentityVerificationDocumentResponse(
    long Id,
    KycDocumentType DocumentType,
    string? DocumentNumber,
    LicenseClass? LicenseClass,
    string? FrontImageUrl,
    string? BackImageUrl,
    string? FileUrl,
    DateOnly? IssueDate,
    DateOnly? ExpiryDate,
    KycStatus KycStatus,
    DateTime CreatedAt,
    DateTime? VerifiedAt,
    string? RejectionReason,
    IdentityOcrResult? OcrResult);
