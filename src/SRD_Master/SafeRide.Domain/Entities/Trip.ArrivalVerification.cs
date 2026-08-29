namespace SafeRide.Domain.Entities;

public partial class Trip
{
    public decimal? ArrivalLatitude { get; set; }
    public decimal? ArrivalLongitude { get; set; }
    public decimal? ArrivalDistanceMeters { get; set; }
    public DateTime? ArrivalLocationVerifiedAt { get; set; }
    public DateTime? CustomerNoShowReminderSentAt { get; set; }
}
