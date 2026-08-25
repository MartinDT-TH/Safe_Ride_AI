using MediatR;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Bookings.Queries.EstimateBookingFare;

public sealed record EstimateBookingFareQuery(
    Guid CustomerId,
    long VehicleId,
    long ServiceTypeId,
    BookingType BookingType,
    DateTime? ScheduledAt,
    double PickupLatitude,
    double PickupLongitude,
    double DestinationLatitude,
    double DestinationLongitude,
    int? EstimatedHours) : IRequest<EstimateBookingFareResult>;
