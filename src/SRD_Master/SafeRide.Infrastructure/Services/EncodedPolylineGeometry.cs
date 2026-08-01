using SafeRide.Application.Common.Models;

namespace SafeRide.Infrastructure.Services;

internal static class EncodedPolylineGeometry
{
    public static IReadOnlyList<LocationPoint> Decode(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return [];
        }

        var points = new List<LocationPoint>();
        var index = 0;
        var latitude = 0;
        var longitude = 0;

        while (index < encoded.Length)
        {
            latitude += DecodeValue(encoded, ref index);
            longitude += DecodeValue(encoded, ref index);
            points.Add(new LocationPoint(latitude / 1e5, longitude / 1e5));
        }

        return points;
    }

    public static double DistanceToRouteMeters(
        LocationPoint point,
        IReadOnlyList<LocationPoint> route)
        => Project(point, route).DistanceToRouteMeters;

    public static RouteProjection Project(
        LocationPoint point,
        IReadOnlyList<LocationPoint> route)
    {
        if (route.Count < 2)
        {
            return new RouteProjection(
                double.PositiveInfinity,
                0,
                0);
        }

        var minimum = double.PositiveInfinity;
        var progressAtMinimum = 0d;
        var accumulated = 0d;
        for (var index = 0; index < route.Count - 1; index++)
        {
            var segment = ProjectToSegment(point, route[index], route[index + 1]);
            if (segment.DistanceMeters < minimum)
            {
                minimum = segment.DistanceMeters;
                progressAtMinimum =
                    accumulated + segment.SegmentLengthMeters * segment.Fraction;
            }
            accumulated += segment.SegmentLengthMeters;
        }

        return new RouteProjection(minimum, progressAtMinimum, accumulated);
    }

    private static int DecodeValue(string encoded, ref int index)
    {
        var result = 0;
        var shift = 0;
        int value;
        do
        {
            if (index >= encoded.Length)
            {
                throw new FormatException("Encoded polyline is incomplete.");
            }

            value = encoded[index++] - 63;
            result |= (value & 0x1f) << shift;
            shift += 5;
        }
        while (value >= 0x20);

        return (result & 1) != 0 ? ~(result >> 1) : result >> 1;
    }

    private static SegmentProjection ProjectToSegment(
        LocationPoint point,
        LocationPoint start,
        LocationPoint end)
    {
        const double metersPerDegreeLatitude = 111_320d;
        var latitudeRadians = point.Latitude * Math.PI / 180d;
        var metersPerDegreeLongitude =
            metersPerDegreeLatitude * Math.Cos(latitudeRadians);

        var startX = (start.Longitude - point.Longitude) * metersPerDegreeLongitude;
        var startY = (start.Latitude - point.Latitude) * metersPerDegreeLatitude;
        var endX = (end.Longitude - point.Longitude) * metersPerDegreeLongitude;
        var endY = (end.Latitude - point.Latitude) * metersPerDegreeLatitude;
        var segmentX = endX - startX;
        var segmentY = endY - startY;
        var lengthSquared = segmentX * segmentX + segmentY * segmentY;
        var projection = lengthSquared <= double.Epsilon
            ? 0
            : Math.Clamp(
                -(startX * segmentX + startY * segmentY) / lengthSquared,
                0,
                1);
        var closestX = startX + projection * segmentX;
        var closestY = startY + projection * segmentY;
        return new SegmentProjection(
            Math.Sqrt(closestX * closestX + closestY * closestY),
            Math.Sqrt(lengthSquared),
            projection);
    }

    internal sealed record RouteProjection(
        double DistanceToRouteMeters,
        double ProgressMeters,
        double TotalRouteMeters);

    private sealed record SegmentProjection(
        double DistanceMeters,
        double SegmentLengthMeters,
        double Fraction);
}
