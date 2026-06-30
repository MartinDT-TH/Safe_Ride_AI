using MediatR;
using SafeRide.Contracts.Responses.Drivers;

namespace SafeRide.Application.Features.Drivers.Queries.GetNearbyDrivers;

public sealed record GetNearbyDriversQuery(
    double Latitude,
    double Longitude,
    double RadiusKm,
    int Limit) : IRequest<IReadOnlyList<NearbyDriverResponse>>;
