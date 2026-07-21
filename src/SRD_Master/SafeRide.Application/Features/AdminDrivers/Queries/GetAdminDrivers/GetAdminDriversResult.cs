using SafeRide.Application.Features.AdminDrivers;

namespace SafeRide.Application.Features.AdminDrivers.Queries.GetAdminDrivers;

public sealed record GetAdminDriversResult(
    IReadOnlyList<AdminDriverResponse> Drivers,
    AdminDriverCountsResponse Counts);
