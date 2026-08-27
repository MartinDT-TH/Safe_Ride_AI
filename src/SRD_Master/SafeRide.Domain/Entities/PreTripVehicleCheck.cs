using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class PreTripVehicleCheck
{
    public long Id { get; set; }
    public long TripId { get; set; }
    public Guid DriverId { get; set; }
    public bool BrakeResponsePassed { get; set; }
    public bool FrontRearLightsPassed { get; set; }
    public bool TurnSignalsPassed { get; set; }
    public bool VisibleTiresPassed { get; set; }
    public bool DashboardWarningPassed { get; set; }
    public bool WindshieldVisibilityPassed { get; set; }
    public bool NoMajorVisibleIssue { get; set; }
    public PreTripCheckResult Result { get; set; }
    public VehicleFaultType? FaultType { get; set; }
    public string? Note { get; set; }
    public string? EvidenceUrl { get; set; }
    public string? EvidenceStoragePublicId { get; set; }
    public string? EvidenceOriginalFileName { get; set; }
    public string? EvidenceContentType { get; set; }
    public long? EvidenceFileSizeBytes { get; set; }
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
    public Trip Trip { get; set; } = null!;
}
