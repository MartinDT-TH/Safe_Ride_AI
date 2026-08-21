namespace SafeRide.Realtime;

public static class RealtimeGroups
{
    public const string AdminReports = "admin:reports";

    public const string AdminSOS = "admin:sos";

    public const string ManagementAccidents = "management:accidents";

    public static string User(Guid userId) => $"user:{userId}";

    public static string Driver(Guid driverId) => $"driver:{driverId}";

    public static string Booking(long bookingId) => $"booking:{bookingId}";

    public static string Trip(long tripId) => $"trip:{tripId}";

    public static string TripShare(long tripShareId) => $"trip-share:{tripShareId}";
  
    public static string TripChat(long tripId) => $"trip-chat:{tripId}";
}
