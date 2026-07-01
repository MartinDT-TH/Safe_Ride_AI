using MediatR;
using SafeRide.Application.Features.Drivers.DTOs;

namespace SafeRide.Application.Features.Drivers.Queries.GetActiveDriverTrip;

public sealed record GetActiveDriverTripQuery(Guid DriverId)
    : IRequest<ActiveDriverTripDto?>;
