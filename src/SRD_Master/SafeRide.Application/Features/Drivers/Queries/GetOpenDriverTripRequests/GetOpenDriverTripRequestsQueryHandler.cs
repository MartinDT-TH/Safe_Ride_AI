using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Drivers.DTOs;

namespace SafeRide.Application.Features.Drivers.Queries.GetOpenDriverTripRequests;

public sealed class GetOpenDriverTripRequestsQueryHandler
    : IRequestHandler<GetOpenDriverTripRequestsQuery, IReadOnlyList<DriverTripRequestDto>>
{
    private readonly IDriverQueryService _driverQueryService;

    public GetOpenDriverTripRequestsQueryHandler(
        IDriverQueryService driverQueryService)
    {
        _driverQueryService = driverQueryService;
    }

    public Task<IReadOnlyList<DriverTripRequestDto>> Handle(
        GetOpenDriverTripRequestsQuery request,
        CancellationToken cancellationToken)
    {
        return _driverQueryService.GetOpenTripRequestsAsync(
            request.DriverId,
            cancellationToken);
    }
}
