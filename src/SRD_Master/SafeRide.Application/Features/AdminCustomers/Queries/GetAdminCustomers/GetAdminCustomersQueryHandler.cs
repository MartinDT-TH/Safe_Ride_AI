using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.AdminCustomers.Queries.GetAdminCustomers;

public sealed class GetAdminCustomersQueryHandler
    : IRequestHandler<GetAdminCustomersQuery, GetAdminCustomersResult>
{
    private readonly IAdminCustomerAccountService _adminCustomerAccountService;

    public GetAdminCustomersQueryHandler(IAdminCustomerAccountService adminCustomerAccountService)
    {
        _adminCustomerAccountService = adminCustomerAccountService;
    }

    public Task<GetAdminCustomersResult> Handle(
        GetAdminCustomersQuery request,
        CancellationToken cancellationToken)
    {
        return _adminCustomerAccountService.GetCustomersAsync(cancellationToken);
    }
}
