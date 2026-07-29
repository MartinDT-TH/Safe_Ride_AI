using MediatR;
using SafeRide.Application.Features.Drivers.DTOs;

namespace SafeRide.Application.Features.Drivers.Queries.GetOpenDriverTripRequests;

public sealed record GetOpenDriverTripRequestsQuery(Guid DriverId)
    : IRequest<IReadOnlyList<DriverTripRequestDto>>;
