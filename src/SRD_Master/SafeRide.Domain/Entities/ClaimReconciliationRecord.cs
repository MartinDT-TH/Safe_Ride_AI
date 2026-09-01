using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class ClaimReconciliationRecord
{
    public long Id { get; set; }
    public long ProtectionClaimId { get; set; }
    public ClaimReconciliationType ReconciliationType { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = null!;
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
