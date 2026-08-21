using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public partial class Trip
{
    public TripTerminationCategory? TerminationCategory { get; set; }
    public string? SafetyTerminationReason { get; set; }
    public DateTime? SafetyTerminatedAt { get; set; }
    public SafetyPaymentReconciliation? SafetyPaymentReconciliation { get; set; }
}

public partial class Report
{
    public SafetyReportType ReportType { get; set; } = SafetyReportType.GENERAL;
    public string? ReasonCode { get; set; }
    public DateTime? OccurredAtUtc { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool EscalationRequested { get; set; }
    public long? PreTripVehicleCheckId { get; set; }
}
