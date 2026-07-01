using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Contracts.Responses.Drivers;

namespace SafeRide.Application.Features.Drivers.Queries.GetNearbyDrivers;

public sealed class GetNearbyDriversQueryHandler
    : IRequestHandler<GetNearbyDriversQuery, IReadOnlyList<NearbyDriverResponse>>
{
    private readonly IDriverQueryService _driverQueryService;

    public GetNearbyDriversQueryHandler(IDriverQueryService driverQueryService)
    {
        _driverQueryService = driverQueryService;
    }

    public Task<IReadOnlyList<NearbyDriverResponse>> Handle(
        GetNearbyDriversQuery request,
        CancellationToken cancellationToken)
    {
        return _driverQueryService.GetNearbyDriversAsync(
            request.Latitude,
            request.Longitude,
            request.RadiusKm,
            request.Limit,
            cancellationToken);
    }
}
