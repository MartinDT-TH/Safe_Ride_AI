using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.AdminDrivers.Commands.BlockAdminDriver;

public sealed class BlockAdminDriverCommandHandler
    : IRequestHandler<BlockAdminDriverCommand, AdminDriverResponse>
{
    private readonly IAdminDriverAccountService _adminDriverAccountService;

    public BlockAdminDriverCommandHandler(IAdminDriverAccountService adminDriverAccountService)
    {
        _adminDriverAccountService = adminDriverAccountService;
    }

    public Task<AdminDriverResponse> Handle(
        BlockAdminDriverCommand request,
        CancellationToken cancellationToken)
    {
        return _adminDriverAccountService.BlockDriverAsync(
            request.DriverId,
            request.Reason,
            cancellationToken);
    }
}
