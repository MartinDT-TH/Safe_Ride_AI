using System.ComponentModel.DataAnnotations;

namespace SafeRide.Contracts.Requests.Drivers;

public sealed record UpdateDriverLocationRequest(
    [property: Range(-90d, 90d)] double Latitude,
    [property: Range(-180d, 180d)] double Longitude);
