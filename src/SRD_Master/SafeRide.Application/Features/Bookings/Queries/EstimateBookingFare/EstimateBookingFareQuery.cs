using MediatR;

namespace SafeRide.Application.Features.Bookings.Queries.EstimateBookingFare;

public sealed record EstimateBookingFareQuery(
    Guid CustomerId,
    long VehicleId,
    long ServiceTypeId,
    double PickupLatitude,
    double PickupLongitude,
    double DestinationLatitude,
    double DestinationLongitude,
    int? EstimatedHours) : IRequest<EstimateBookingFareResult>;
