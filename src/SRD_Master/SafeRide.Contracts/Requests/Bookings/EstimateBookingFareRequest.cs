using System.ComponentModel.DataAnnotations;
using SafeRide.Domain.Enums;

namespace SafeRide.Contracts.Requests.Bookings;

public sealed class EstimateBookingFareRequest
{
    public BookingType BookingType { get; init; } = BookingType.Now;

    public DateTime? ScheduledAt { get; init; }

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

    [Range(1, 24)]
    public int? EstimatedHours { get; init; }
}
