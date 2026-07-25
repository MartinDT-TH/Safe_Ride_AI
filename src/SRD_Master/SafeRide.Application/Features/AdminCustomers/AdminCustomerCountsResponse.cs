namespace SafeRide.Application.Features.AdminCustomers;

public sealed record AdminCustomerCountsResponse(
    int All,
    int Active,
    int Blocked,
    int Premium);
