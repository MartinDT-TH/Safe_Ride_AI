using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.AdminCustomers.Commands.BlockAdminCustomer;

public sealed class BlockAdminCustomerCommandHandler
    : IRequestHandler<BlockAdminCustomerCommand, AdminCustomerResponse>
{
    private readonly IAdminCustomerAccountService _adminCustomerAccountService;

    public BlockAdminCustomerCommandHandler(IAdminCustomerAccountService adminCustomerAccountService)
    {
        _adminCustomerAccountService = adminCustomerAccountService;
    }

    public Task<AdminCustomerResponse> Handle(
        BlockAdminCustomerCommand request,
        CancellationToken cancellationToken)
    {
        return _adminCustomerAccountService.BlockCustomerAsync(
            request.CustomerId,
            request.Reason,
            cancellationToken);
    }
}
