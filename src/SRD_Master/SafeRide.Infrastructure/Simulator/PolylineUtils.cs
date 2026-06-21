namespace SafeRide.Infrastructure.Simulator;

public static class PolylineUtils
{
    /// <summary>
    /// Decodes an encoded polyline string into a list of GPS coordinates.
    /// Supports both Google and OpenRouteService polylines.
    /// </summary>
    public static List<(double Lat, double Lng)> Decode(string encodedPolyline)
    {
        if (string.IsNullOrEmpty(encodedPolyline))
            return new List<(double, double)>();

        var polyline = new List<(double, double)>();
        int index = 0;
        int lat = 0;
        int lng = 0;

        while (index < encodedPolyline.Length)
        {
            int b;
            int shift = 0;
            int result = 0;
            do
            {
                b = encodedPolyline[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);
            int dlat = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
            lat += dlat;

            shift = 0;
            result = 0;
            do
            {
                b = encodedPolyline[index++] - 63;
                result |= (b & 0x1f) << shift;
                shift += 5;
            } while (b >= 0x20);
            int dlng = ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
            lng += dlng;

            polyline.Add((lat * 1e-5, lng * 1e-5));
        }

        return polyline;
    }

    /// <summary>
    /// Calculates the Haversine distance between two GPS points in meters.
    /// </summary>
    public static double GetDistance(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371000; // Earth radius in meters
        var dLat = ToRadians(lat2 - lat1);
        var dLng = ToRadians(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    /// <summary>
    /// Calculates the total distance of a path.
    /// </summary>
    public static double CalculateTotalDistance(List<(double Lat, double Lng)> path)
    {
        double total = 0;
        for (int i = 0; i < path.Count - 1; i++)
        {
            total += GetDistance(path[i].Lat, path[i].Lng, path[i + 1].Lat, path[i + 1].Lng);
        }
        return total;
    }

    /// <summary>
    /// Interpolates a point on the path at a specific distance from the start.
    /// </summary>
    public static (double Lat, double Lng) GetPointAtDistance(List<(double Lat, double Lng)> path, double distance)
    {
        if (path == null || path.Count == 0) return (0, 0);
        if (distance <= 0) return path[0];

        double accumulatedDistance = 0;
        for (int i = 0; i < path.Count - 1; i++)
        {
            double segmentDist = GetDistance(path[i].Lat, path[i].Lng, path[i + 1].Lat, path[i + 1].Lng);
            if (accumulatedDistance + segmentDist >= distance)
            {
                double remainingDistance = distance - accumulatedDistance;
                double ratio = remainingDistance / segmentDist;
                double lat = path[i].Lat + (path[i + 1].Lat - path[i].Lat) * ratio;
                double lng = path[i].Lng + (path[i + 1].Lng - path[i].Lng) * ratio;
                return (lat, lng);
            }
            accumulatedDistance += segmentDist;
        }
        return path[^1];
    }

    private static double ToRadians(double degree) => degree * Math.PI / 180;
}
