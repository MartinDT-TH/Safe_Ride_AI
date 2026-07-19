using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Drivers.DTOs;

namespace SafeRide.Application.Features.Drivers.Queries.GetDriverWallet;

public sealed class GetDriverWalletQueryHandler
    : IRequestHandler<GetDriverWalletQuery, DriverWalletDto>
{
    private readonly IDriverQueryService _driverQueryService;

    public GetDriverWalletQueryHandler(IDriverQueryService driverQueryService)
    {
        _driverQueryService = driverQueryService;
    }

    public Task<DriverWalletDto> Handle(
        GetDriverWalletQuery request,
        CancellationToken cancellationToken)
    {
        return _driverQueryService.GetWalletAsync(
            request.DriverId,
            request.Period,
            request.UtcOffsetMinutes,
            request.RecentLimit,
            cancellationToken);
    }
}
