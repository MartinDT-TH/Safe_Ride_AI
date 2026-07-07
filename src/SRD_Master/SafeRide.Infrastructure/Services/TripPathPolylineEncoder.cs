using System.Text;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.Infrastructure.Services;

internal static class TripPathPolylineEncoder
{
    public static string? Encode(IReadOnlyList<TripTrackingPoint> points)
    {
        if (points.Count < 2)
        {
            return null;
        }

        var builder = new StringBuilder();
        long previousLatitude = 0;
        long previousLongitude = 0;

        foreach (var point in points)
        {
            var latitude = (long)Math.Round(point.Latitude * 1e5);
            var longitude = (long)Math.Round(point.Longitude * 1e5);

            EncodeValue(latitude - previousLatitude, builder);
            EncodeValue(longitude - previousLongitude, builder);

            previousLatitude = latitude;
            previousLongitude = longitude;
        }

        return builder.ToString();
    }

    private static void EncodeValue(long value, StringBuilder builder)
    {
        value = value < 0 ? ~(value << 1) : value << 1;
        while (value >= 0x20)
        {
            builder.Append((char)((0x20 | (value & 0x1f)) + 63));
            value >>= 5;
        }

        builder.Append((char)(value + 63));
    }
}
