namespace SafeRide.Domain.Entities;

public sealed class LiabilityDisputeAudit
{
    public long Id { get; set; }
    public long AssessmentId { get; set; }
    public Guid DisputedByUserId { get; set; }
    public DateTime DisputedAtUtc { get; set; }
    public string Reason { get; set; } = null!;
    public AccidentLiabilityAssessment Assessment { get; set; } = null!;
    public ICollection<LiabilityDisputeEvidence> Evidence { get; set; } = new List<LiabilityDisputeEvidence>();
}

public sealed class LiabilityDisputeEvidence
{
    public long LiabilityDisputeAuditId { get; set; }
    public long AccidentEvidenceId { get; set; }
    public LiabilityDisputeAudit LiabilityDisputeAudit { get; set; } = null!;
    public AccidentEvidence AccidentEvidence { get; set; } = null!;
}
