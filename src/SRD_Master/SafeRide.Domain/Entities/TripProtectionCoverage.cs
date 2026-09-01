namespace SafeRide.Domain.Entities;

public sealed class TripProtectionCoverage
{
    public long Id { get; set; }
    public long TripId { get; set; }
    public long PolicyVersionId { get; set; }
    public long PreTripVehicleCheckId { get; set; }
    public decimal ProtectionLimit { get; set; }
    public DateTime ActivatedAtUtc { get; set; }
    public Trip Trip { get; set; } = null!;
    public RiskProtectionPolicyVersion PolicyVersion { get; set; } = null!;
    public PreTripVehicleCheck PreTripVehicleCheck { get; set; } = null!;
}
