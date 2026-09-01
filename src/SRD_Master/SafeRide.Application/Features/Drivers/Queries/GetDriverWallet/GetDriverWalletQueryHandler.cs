using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Drivers.DTOs;

namespace SafeRide.Application.Features.Drivers.Queries.GetDriverWallet;

public sealed class GetDriverWalletQueryHandler
    : IRequestHandler<GetDriverWalletQuery, DriverWalletDto>
{
    private readonly IDriverQueryService _driverQueryService;
    private readonly IDriverWalletTopUpService _walletTopUpService;

    public GetDriverWalletQueryHandler(
        IDriverQueryService driverQueryService,
        IDriverWalletTopUpService walletTopUpService)
    {
        _driverQueryService = driverQueryService;
        _walletTopUpService = walletTopUpService;
    }

    public async Task<DriverWalletDto> Handle(
        GetDriverWalletQuery request,
        CancellationToken cancellationToken)
    {
        await _walletTopUpService.ReconcilePendingAsync(
            request.DriverId,
            cancellationToken);

        return await _driverQueryService.GetWalletAsync(
            request.DriverId,
            request.Period,
            request.UtcOffsetMinutes,
            request.RecentLimit,
            cancellationToken);
    }
}
