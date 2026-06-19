using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.IdentityVerification.DTOs;

public sealed record IdentityOcrResult(
    KycDocumentType DocumentType,
    string RawText,
    decimal Confidence,
    string? DocumentNumber,
    LicenseClass? LicenseClass,
    DateOnly? IssueDate,
    DateOnly? ExpiryDate,
    IReadOnlyDictionary<string, string?> Fields);

