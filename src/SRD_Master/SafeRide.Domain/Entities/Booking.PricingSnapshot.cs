#nullable enable

namespace SafeRide.Domain.Entities;

public partial class Booking
{
    public const int CurrentPricingSnapshotVersion = 1;

    public int? PricingSnapshotVersion { get; set; }

    public decimal? AcceptedBaseFare { get; set; }

    public decimal? AcceptedMinimumServiceFare { get; set; }

    public decimal? AcceptedPricePerKm { get; set; }

    public decimal? AcceptedPricePerHour { get; set; }

    public decimal? AcceptedSurgeMultiplier { get; set; }

    public DateTime? SurgeEvaluationTime { get; set; }

    public decimal? NormalFare { get; set; }

    public decimal? SurgedFare { get; set; }

    public decimal? SurgeAmount { get; set; }

    public decimal? AcceptedLongDistanceThresholdKm { get; set; }

    public decimal? AcceptedLongDistanceOptInThresholdKm { get; set; }

    public decimal? AcceptedMaximumTripDistanceKm { get; set; }

    public decimal? AcceptedLongDistanceRatePerKm { get; set; }

    public decimal? LongDistanceComponent { get; set; }
}
