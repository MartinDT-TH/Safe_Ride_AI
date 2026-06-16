using System.ComponentModel.DataAnnotations;

namespace SafeRide.Contracts.Requests.Bookings;

public sealed class EstimateBookingFareRequest
{
    [Range(1, long.MaxValue)]
    public long VehicleId { get; init; }

    [Range(1, long.MaxValue)]
    public long ServiceTypeId { get; init; }

    [Range(-90, 90)]
    public double PickupLatitude { get; init; }

    [Range(-180, 180)]
    public double PickupLongitude { get; init; }

    [Range(-90, 90)]
    public double DestinationLatitude { get; init; }

    [Range(-180, 180)]
    public double DestinationLongitude { get; init; }
}
