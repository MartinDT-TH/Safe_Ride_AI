using System.ComponentModel.DataAnnotations;

namespace SafeRide.Contracts.Requests.Drivers;

public sealed record UpdateDriverLocationRequest(
    [Range(-90d, 90d)] double Latitude,
    [Range(-180d, 180d)] double Longitude);
