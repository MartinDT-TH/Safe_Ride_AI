using System.ComponentModel.DataAnnotations;

namespace SafeRide.Contracts.Requests.Drivers;

public sealed record UpdateDriverLocationRequest
{
    public UpdateDriverLocationRequest(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    [Range(-90d, 90d)]
    public double Latitude { get; init; }

    [Range(-180d, 180d)]
    public double Longitude { get; init; }

    public DateTime? ClientTimestampUtc { get; init; }

    [Range(0, long.MaxValue)]
    public long? Sequence { get; init; }

    [Range(0d, double.MaxValue)]
    public double? AccuracyMeters { get; init; }

    [Range(0d, double.MaxValue)]
    public double? SpeedMetersPerSecond { get; init; }
}
