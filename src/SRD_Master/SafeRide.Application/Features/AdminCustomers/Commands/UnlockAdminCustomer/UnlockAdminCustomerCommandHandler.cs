using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.AdminCustomers.Commands.UnlockAdminCustomer;

public sealed class UnlockAdminCustomerCommandHandler
    : IRequestHandler<UnlockAdminCustomerCommand, AdminCustomerResponse>
{
    private readonly IAdminCustomerAccountService _adminCustomerAccountService;

    public UnlockAdminCustomerCommandHandler(IAdminCustomerAccountService adminCustomerAccountService)
    {
        _adminCustomerAccountService = adminCustomerAccountService;
    }

    public Task<AdminCustomerResponse> Handle(
        UnlockAdminCustomerCommand request,
        CancellationToken cancellationToken)
    {
        return _adminCustomerAccountService.UnlockCustomerAsync(
            request.CustomerId,
            cancellationToken);
    }
}
