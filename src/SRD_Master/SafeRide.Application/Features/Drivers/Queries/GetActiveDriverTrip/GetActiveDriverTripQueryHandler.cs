using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Drivers.DTOs;

namespace SafeRide.Application.Features.Drivers.Queries.GetActiveDriverTrip;

public sealed class GetActiveDriverTripQueryHandler
    : IRequestHandler<GetActiveDriverTripQuery, ActiveDriverTripDto?>
{
    private readonly IDriverQueryService _driverQueryService;

    public GetActiveDriverTripQueryHandler(IDriverQueryService driverQueryService)
    {
        _driverQueryService = driverQueryService;
    }

    public Task<ActiveDriverTripDto?> Handle(
        GetActiveDriverTripQuery request,
        CancellationToken cancellationToken)
    {
        return _driverQueryService.GetActiveTripAsync(
            request.DriverId,
            cancellationToken);
    }
}
