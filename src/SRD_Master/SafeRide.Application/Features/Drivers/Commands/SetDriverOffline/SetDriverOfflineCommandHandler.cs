using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.Drivers.Commands.SetDriverOffline;

public sealed class SetDriverOfflineCommandHandler
    : IRequestHandler<SetDriverOfflineCommand, SetDriverOfflineResult>
{
    private readonly IDriverQueryService _driverQueryService;
    private readonly IDriverRealtimeService _driverRealtimeService;

    public SetDriverOfflineCommandHandler(
        IDriverQueryService driverQueryService,
        IDriverRealtimeService driverRealtimeService)
    {
        _driverQueryService = driverQueryService;
        _driverRealtimeService = driverRealtimeService;
    }

    public async Task<SetDriverOfflineResult> Handle(
        SetDriverOfflineCommand request,
        CancellationToken cancellationToken)
    {
        var isBusy = await _driverQueryService.HasActiveTripOrBusyStatusAsync(
            request.DriverId,
            cancellationToken);

        if (isBusy)
        {
            return new SetDriverOfflineResult(CanSetOffline: false);
        }

        await _driverRealtimeService.SetDriverOfflineAsync(
            request.DriverId,
            cancellationToken);

        return new SetDriverOfflineResult(CanSetOffline: true);
    }
}
