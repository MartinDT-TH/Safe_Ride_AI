namespace SafeRide.Application.Common.Models;

public sealed class CustomerNoShowOptions
{
    public const string SectionName = "CustomerNoShow";
    public int NoShowWaitMinutes { get; set; } = 10;
    public int ArrivalRadiusMeters { get; set; } = 100;
    public int DriverLocationFreshnessSeconds { get; set; } = 120;
    public double DriverSupportMinPickupDistanceKm { get; set; } = 5.0;
    public decimal DriverNoShowSupportAmount { get; set; } = 10000m;
    public int BehaviorWindowDays { get; set; } = 30;
    public int ScheduleRestrictionDaysFirst { get; set; } = 7;
    public int ScheduleRestrictionDaysPersistent { get; set; } = 14;
    public int InstantCooldownHoursPersistent { get; set; } = 2;
}
