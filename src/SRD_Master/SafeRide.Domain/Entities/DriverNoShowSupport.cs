using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class DriverNoShowSupport
{
    public long Id { get; set; }
    public long TripId { get; set; }
    public long BookingId { get; set; }
    public Guid DriverId { get; set; }
    public long CustomerBehaviorEventId { get; set; }
    public decimal AcceptedPickupDistanceKm { get; set; }
    public decimal SupportAmount { get; set; }
    public DriverNoShowSupportStatus Status { get; set; } = DriverNoShowSupportStatus.PENDING;
    public long? WalletTransactionId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime? ReversedAt { get; set; }

    public Trip Trip { get; set; } = null!;
    public Booking Booking { get; set; } = null!;
    public DriverProfile Driver { get; set; } = null!;
    public CustomerBehaviorEvent CustomerBehaviorEvent { get; set; } = null!;
    public WalletTransaction? WalletTransaction { get; set; }
}
