using SafeRide.Domain.Enums;

namespace SafeRide.Domain.Entities;

public sealed class CustomerBehaviorEvent
{
    public long Id { get; set; }
    public Guid CustomerId { get; set; }
    public long BookingId { get; set; }
    public long TripId { get; set; }
    public CustomerBehaviorEventType EventType { get; set; }
    public CustomerBehaviorEventStatus Status { get; set; }
    public Guid DriverId { get; set; }
    public DateTime? DriverReportedAt { get; set; }
    public DateTime? ArrivedAt { get; set; }
    public decimal? ArrivalLatitude { get; set; }
    public decimal? ArrivalLongitude { get; set; }
    public decimal? ArrivalDistanceMeters { get; set; }
    public DateTime? ReminderSentAt { get; set; }
    public DateTime? WaitSatisfiedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime? ExemptedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public AspNetUser Customer { get; set; } = null!;
    public Booking Booking { get; set; } = null!;
    public Trip Trip { get; set; } = null!;
    public DriverProfile Driver { get; set; } = null!;
    public AspNetUser? ReviewedByUser { get; set; }
}
