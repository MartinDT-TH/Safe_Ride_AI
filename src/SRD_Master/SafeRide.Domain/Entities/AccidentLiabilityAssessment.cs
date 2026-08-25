using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class AccidentLiabilityAssessment
{
    public long Id { get; set; }
    public long AccidentReportId { get; set; }
    public decimal DriverFaultPercentage { get; set; }
    public decimal CustomerFaultPercentage { get; set; }
    public decimal ThirdPartyFaultPercentage { get; set; }
    public decimal VehicleFailurePercentage { get; set; }
    public decimal ObjectiveCausePercentage { get; set; }
    public DriverFaultLevel DriverFaultLevel { get; set; }
    public VehicleDefectAwareness VehicleDefectAwareness { get; set; }
    public LiabilityAssessmentStatus Status { get; set; } = LiabilityAssessmentStatus.DRAFT;
    public Guid? ConfirmedByUserId { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }
    public string? DisputeReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public AccidentReport AccidentReport { get; set; } = null!;
    public ICollection<AccidentLiabilityCause> Causes { get; set; } = new List<AccidentLiabilityCause>();
    public ICollection<LiabilityDisputeAudit> Disputes { get; set; } = new List<LiabilityDisputeAudit>();
}
