using MediatR;

namespace SafeRide.Application.Features.AdminCustomers.Commands.UnlockAdminCustomer;

public sealed record UnlockAdminCustomerCommand(
    Guid CustomerId) : IRequest<AdminCustomerResponse>;
