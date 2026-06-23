namespace SafeRide.Infrastructure.Redis;

public static class RedisKeys
{
    public static string Otp(string phoneNumber) =>
        $"auth:otp:{phoneNumber}";

    public static string OtpAttempts(string phoneNumber) =>
        $"auth:otp:attempts:{phoneNumber}";

    public static string OtpLock(string phoneNumber) =>
        $"auth:otp:lock:{phoneNumber}";

    public static string OtpSendCooldown(string phoneNumber) =>
        $"auth:otp:send-cooldown:{phoneNumber}";

    public static string RefreshToken(string tokenHash) =>
        $"auth:refresh-token:{tokenHash}";

    public static string DriverLocation(Guid driverId) =>
        $"sr:driver:location:{driverId}";

    public static string DriverOnline(Guid driverId) =>
        $"sr:driver:online:{driverId}";

    public static string DriverStatus(Guid driverId) =>
        $"sr:driver:status:{driverId}";

    public const string OnlineDriversGeo = "sr:geo:drivers:online";

    public static string MatchingBooking(long bookingId) =>
        $"sr:matching:booking:{bookingId}";

    public static string MatchingOffer(long bookingId, Guid driverId) =>
        $"sr:matching:offer:{bookingId}:{driverId}";

    public static string MatchingDriverLock(Guid driverId) =>
        $"sr:matching:driver-lock:{driverId}";

    public static string BookingRadiusExpandedNotified(long bookingId) =>
        $"sr:booking:radius-expanded-notified:{bookingId}";

    public static string HangfireExpandRadiusJobId(long bookingId) =>
        $"sr:booking:hf-expand-job:{bookingId}";

    public static string HangfireExpireBookingJobId(long bookingId) =>
        $"sr:booking:hf-expire-job:{bookingId}";

    public static string HangfireExpireDriverOfferJobId(long offerId) =>
        $"sr:booking:hf-expire-offer-job:{offerId}";

    public static string TripLive(long tripId) =>
        $"sr:trip:live:{tripId}";

    public const string ActivePricingRules = "sr:pricing:rules:active";

    public static string SignalRUser(Guid userId) =>
        $"sr:signalr:user:{userId}";

    public static string SignalRDriver(Guid driverId) =>
        $"sr:signalr:driver:{driverId}";
}
