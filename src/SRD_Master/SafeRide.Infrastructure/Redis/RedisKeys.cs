using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SafeRide.Infrastructure.Redis;

public static class RedisKeys
{
    public static string Otp(string phoneNumber)
        => $"otp:{phoneNumber}";

    public static string RefreshToken(string tokenHash)
        => $"auth:refresh:{tokenHash}";

    public static string UserConnection(Guid userId)
        => $"signalr:user:{userId}";

    public static string DriverOnline(Guid driverId)
        => $"online:driver:{driverId}";

    public static string DriverLocation(Guid driverId)
        => $"driver:location:{driverId}";

    public static string DispatchLock(Guid bookingId)
        => $"dispatch:booking:{bookingId}";

    public const string DriverGeoKey = "geo:drivers";
}
