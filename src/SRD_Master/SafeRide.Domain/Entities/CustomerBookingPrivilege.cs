using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class CustomerBookingPrivilege
{
    public Guid CustomerId { get; set; }
    public bool ScheduledBookingAllowed { get; set; } = true;
    public DateTime? ScheduledRestrictedUntil { get; set; }
    public bool InstantBookingAllowed { get; set; } = true;
    public DateTime? BookingCooldownUntil { get; set; }
    public int VerifiedNoShowCount { get; set; }
    public int EligibleBookingCount { get; set; }
    public decimal NoShowRate { get; set; }
    public int ConsecutiveNoShowStreak { get; set; }
    public DateTime? LastNoShowAt { get; set; }
    public CustomerBehaviorRestrictionLevel RestrictionLevel { get; set; } = CustomerBehaviorRestrictionLevel.NORMAL;
    public bool UnderStaffReview { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public AspNetUser Customer { get; set; } = null!;
}
