using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class AccidentReport
{
    public long Id { get; set; }
    public long TripId { get; set; }
    public Guid ReportedByUserId { get; set; }
    public AccidentCategory Category { get; set; }
    public AccidentStatus Status { get; set; } = AccidentStatus.REPORTED;
    public DateTime OccurredAtUtc { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string Description { get; set; } = null!;
    public string? PoliceReportReference { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Trip Trip { get; set; } = null!;
    public ICollection<AccidentEvidence> Evidence { get; set; } = new List<AccidentEvidence>();
    public AccidentLiabilityAssessment? LiabilityAssessment { get; set; }
    public ProtectionClaim? ProtectionClaim { get; set; }
}
