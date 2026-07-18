using MediatR;
using SafeRide.Application.Common.Interfaces;

namespace SafeRide.Application.Features.AdminDrivers.Queries.GetAdminDrivers;

public sealed class GetAdminDriversQueryHandler
    : IRequestHandler<GetAdminDriversQuery, GetAdminDriversResult>
{
    private readonly IAdminDriverAccountService _adminDriverAccountService;

    public GetAdminDriversQueryHandler(IAdminDriverAccountService adminDriverAccountService)
    {
        _adminDriverAccountService = adminDriverAccountService;
    }

    public Task<GetAdminDriversResult> Handle(
        GetAdminDriversQuery request,
        CancellationToken cancellationToken)
    {
        return _adminDriverAccountService.GetDriversAsync(
            request.Status,
            cancellationToken);
    }
}
