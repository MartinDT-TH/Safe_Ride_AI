using MediatR;

namespace SafeRide.Application.Features.Drivers.Commands.SetDriverOffline;

public sealed record SetDriverOfflineCommand(Guid DriverId)
    : IRequest<SetDriverOfflineResult>;

public sealed record SetDriverOfflineResult(bool CanSetOffline);
