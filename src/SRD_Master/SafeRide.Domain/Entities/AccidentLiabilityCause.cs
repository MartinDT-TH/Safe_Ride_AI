using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class AccidentLiabilityCause
{
    public long Id { get; set; }
    public long AssessmentId { get; set; }
    public AccidentRootCause RootCause { get; set; }
    public ResponsiblePartyType ResponsibleParty { get; set; }
    public decimal Percentage { get; set; }
    public AccidentLiabilityAssessment Assessment { get; set; } = null!;
}
