using SafeRide.Domain.Entities;

namespace SafeRide.Application.Features.AdminDrivers;

public sealed record AdminDriverDocumentResponse(
    long Id,
    string DocumentType,
    string? DocumentNumber,
    string? LicenseClass,
    string? FrontImageUrl,
    string? BackImageUrl,
    string? FileUrl,
    DateOnly? IssueDate,
    DateOnly? ExpiryDate,
    string KycStatus,
    DateTime CreatedAt,
    DateTime? VerifiedAt,
    string? RejectionReason)
{
    public static AdminDriverDocumentResponse From(DriverKyc document) => new(
        document.Id,
        document.DocumentType.ToString(),
        document.DocumentNumber,
        document.LicenseClass?.ToString(),
        document.FrontImageUrl,
        document.BackImageUrl,
        document.FileUrl,
        document.IssueDate,
        document.ExpiryDate,
        document.KycStatus.ToString(),
        document.CreatedAt,
        document.VerifiedAt,
        document.RejectionReason);
}
