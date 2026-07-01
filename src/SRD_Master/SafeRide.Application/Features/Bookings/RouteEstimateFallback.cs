using SafeRide.Application.Common.Models;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Bookings;

internal static class RouteEstimateFallback
{
    private const double EarthRadiusKm = 6371.0088;
    private const double RoadDistanceFactor = 1.25;
    private const double AverageSpeedKmh = 28d;

    public static RouteEstimateResult Create(
        double pickupLatitude,
        double pickupLongitude,
        double destinationLatitude,
        double destinationLongitude,
        MapProvider? fallbackFromProvider = null)
    {
        var straightLineKm = HaversineKm(
            pickupLatitude,
            pickupLongitude,
            destinationLatitude,
            destinationLongitude);
        var distanceKm = Math.Max(0.1d, straightLineKm * RoadDistanceFactor);
        var durationHours = distanceKm / AverageSpeedKmh;
        var durationSeconds = Math.Max(60d, durationHours * 3600d);

        return new RouteEstimateResult
        {
            Provider = MapProvider.Auto,
            DistanceMeters = distanceKm * 1000d,
            DurationSeconds = durationSeconds,
            EncodedPolyline = null,
            PolylineFormat = "polyline5",
            Points = [],
            Summary = "Fallback route estimate",
            IsFallbackResult = true,
            FallbackFromProvider = fallbackFromProvider
        };
    }

    private static double HaversineKm(
        double lat1,
        double lon1,
        double lat2,
        double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var rLat1 = ToRadians(lat1);
        var rLat2 = ToRadians(lat2);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(rLat1) * Math.Cos(rLat2)
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
}
