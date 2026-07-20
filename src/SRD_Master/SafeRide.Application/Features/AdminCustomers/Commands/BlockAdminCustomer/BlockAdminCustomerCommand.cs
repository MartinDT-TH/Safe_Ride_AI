using MediatR;

namespace SafeRide.Application.Features.AdminCustomers.Commands.BlockAdminCustomer;

public sealed record BlockAdminCustomerCommand(
    Guid CustomerId,
    string? Reason) : IRequest<AdminCustomerResponse>;
