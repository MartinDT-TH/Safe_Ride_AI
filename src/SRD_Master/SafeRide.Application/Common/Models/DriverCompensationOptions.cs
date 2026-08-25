namespace SafeRide.Application.Common.Models;

public sealed class DriverCompensationOptions
{
    public const string SectionName = "DriverCompensation";

    public double LongPickupThresholdKm { get; set; }

    public double LongPickupOptInThresholdKm { get; set; }

    public double LongDistanceThresholdKm { get; set; }

    public double LongDistanceOptInThresholdKm { get; set; }

    public double MaximumTripDistanceKm { get; set; }

    public decimal LongPickupRatePerKm { get; set; }

    public decimal LongDistanceRatePerKm { get; set; }

    public double DestinationReachedThresholdMeters { get; set; }
}
