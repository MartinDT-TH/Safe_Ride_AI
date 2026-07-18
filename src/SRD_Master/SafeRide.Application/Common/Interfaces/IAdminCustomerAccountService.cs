using SafeRide.Application.Features.AdminCustomers;
using SafeRide.Application.Features.AdminCustomers.Queries.GetAdminCustomers;

namespace SafeRide.Application.Common.Interfaces;

public interface IAdminCustomerAccountService
{
    Task<GetAdminCustomersResult> GetCustomersAsync(CancellationToken cancellationToken);

    Task<AdminCustomerResponse> BlockCustomerAsync(
        Guid customerId,
        string? reason,
        CancellationToken cancellationToken);

    Task<AdminCustomerResponse> UnlockCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken);
}
