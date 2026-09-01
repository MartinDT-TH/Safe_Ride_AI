using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class ClaimRecovery
{
    public long Id { get; set; }
    public long ProtectionClaimId { get; set; }
    public RecoverySourceType SourceType { get; set; }
    public string PayerReference { get; set; } = null!;
    public decimal Amount { get; set; }
    public string PaymentReference { get; set; } = null!;
    public string EvidenceUrl { get; set; } = null!;
    public string EvidenceStoragePublicId { get; set; } = null!;
    public string EvidenceOriginalFileName { get; set; } = null!;
    public string EvidenceContentType { get; set; } = null!;
    public long EvidenceFileSizeBytes { get; set; }
    public Guid RecordedByUserId { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
    public ProtectionClaim ProtectionClaim { get; set; } = null!;
}
