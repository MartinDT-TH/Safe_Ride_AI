using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

/// <summary>Supporting private evidence; it never changes insurer or settlement state.</summary>
public sealed class InsuranceClaimDocument
{
    public long Id { get; set; }
    public long ProtectionClaimId { get; set; }
    public InsuranceClaimDocumentType DocumentType { get; set; }
    public string StorageObjectKey { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long FileSizeBytes { get; set; }
    public string Sha256Hash { get; set; } = null!;
    public Guid UploadedByUserId { get; set; }
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
    public ProtectionClaim ProtectionClaim { get; set; } = null!;
}
