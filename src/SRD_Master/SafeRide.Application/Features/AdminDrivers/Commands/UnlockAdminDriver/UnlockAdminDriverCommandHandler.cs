using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.AdminDrivers.Commands.UnlockAdminDriver;

public sealed class UnlockAdminDriverCommandHandler
    : IRequestHandler<UnlockAdminDriverCommand, AdminDriverResponse>
{
    private readonly IAdminDriverAccountService _adminDriverAccountService;

    public UnlockAdminDriverCommandHandler(IAdminDriverAccountService adminDriverAccountService)
    {
        _adminDriverAccountService = adminDriverAccountService;
    }

    public Task<AdminDriverResponse> Handle(
        UnlockAdminDriverCommand request,
        CancellationToken cancellationToken)
    {
        return _adminDriverAccountService.UnlockDriverAsync(
            request.DriverId,
            cancellationToken);
    }
}
