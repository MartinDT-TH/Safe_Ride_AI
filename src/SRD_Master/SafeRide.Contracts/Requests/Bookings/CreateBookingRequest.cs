using System.ComponentModel.DataAnnotations;
using SafeRide.Domain.Enums;

namespace SafeRide.Contracts.Requests.Bookings;

public sealed class CreateBookingRequest
{
    public BookingType BookingType { get; init; }
    public DateTime? ScheduledAt { get; init; }

    [Range(1, long.MaxValue)]
    public long VehicleId { get; init; }

    [Range(1, long.MaxValue)]
    public long ServiceTypeId { get; init; }

    [Required, MaxLength(255)]
    public string PickupAddress { get; init; } = string.Empty;

    [Range(-90, 90)]
    public double PickupLatitude { get; init; }

    [Range(-180, 180)]
    public double PickupLongitude { get; init; }

    [MaxLength(255)]
    public string? DestinationAddress { get; init; }

    [Range(-90, 90)]
    public double DestinationLatitude { get; init; }

    [Range(-180, 180)]
    public double DestinationLongitude { get; init; }

    [MaxLength(500)]
    public string? SpecialRequest { get; init; }

    [Range(1, 24)]
    public int? EstimatedHours { get; init; }

    [MaxLength(50)]
    public string? PromotionCode { get; init; }
}
