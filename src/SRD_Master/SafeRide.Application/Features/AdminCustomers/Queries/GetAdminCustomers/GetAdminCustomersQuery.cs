using MediatR;

namespace SafeRide.Application.Features.AdminCustomers.Queries.GetAdminCustomers;

public sealed record GetAdminCustomersQuery : IRequest<GetAdminCustomersResult>;
