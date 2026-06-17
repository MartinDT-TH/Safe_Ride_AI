using MediatR;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Bookings.Commands.CreateBooking;

public sealed record CreateBookingCommand(
    Guid CustomerId,
    long VehicleId,
    long ServiceTypeId,
    BookingType BookingType,
    DateTime? ScheduledAt,
    string PickupAddress,
    double PickupLatitude,
    double PickupLongitude,
    string? DestinationAddress,
    double DestinationLatitude,
    double DestinationLongitude,
    string? SpecialRequest,
    int? EstimatedHours) : IRequest<CreateBookingResponse>;
