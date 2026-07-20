using MediatR;

namespace SafeRide.Application.Features.AdminDrivers.Queries.GetAdminDrivers;

public sealed record GetAdminDriversQuery(
    string Status = "all") : IRequest<GetAdminDriversResult>;
