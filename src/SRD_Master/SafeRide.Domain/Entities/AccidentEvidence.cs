using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class AccidentEvidence
{
    public long Id { get; set; }
    public long AccidentReportId { get; set; }
    public int SequenceNumber { get; set; }
    public Guid UploadedByUserId { get; set; }
    public AccidentEvidenceType EvidenceType { get; set; }
    public string FileUrl { get; set; } = null!;
    public string? OriginalFileName { get; set; }
    public string ContentType { get; set; } = null!;
    public string? StoragePublicId { get; set; }
    public long? FileSizeBytes { get; set; }
    public DateTime? CapturedAtUtc { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public AccidentReport AccidentReport { get; set; } = null!;
    public ICollection<LiabilityDisputeEvidence> LiabilityDisputes { get; set; } = new List<LiabilityDisputeEvidence>();
}
