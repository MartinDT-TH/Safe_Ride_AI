using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class TripEndReconciliationRequest
{
    public long Id { get; set; }
    public long TripId { get; set; }
    public TripEndReason RequestedReason { get; set; }
    public Guid RequestedByDriverId { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public TripEndReconciliationStatus Status { get; set; } = TripEndReconciliationStatus.PENDING;
    public Guid? ResolvedByStaffId { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNote { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Trip Trip { get; set; } = null!;
    public DriverProfile RequestedByDriver { get; set; } = null!;
    public AspNetUser? ResolvedByStaff { get; set; }
}
