using SafeRide.Application.Features.AdminCustomers;

namespace SafeRide.Application.Features.AdminCustomers.Queries.GetAdminCustomers;

public sealed record GetAdminCustomersResult(
    IReadOnlyList<AdminCustomerResponse> Customers,
    AdminCustomerCountsResponse Counts);
