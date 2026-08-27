namespace SafeRide.Domain.Entities;

public sealed class SafetyTerminationEvidence
{
    public long Id { get; set; }
    public long TripId { get; set; }
    public string EvidenceUrl { get; set; } = null!;
    public string StoragePublicId { get; set; } = null!;
    public string OriginalFileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long FileSizeBytes { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Trip Trip { get; set; } = null!;
}
