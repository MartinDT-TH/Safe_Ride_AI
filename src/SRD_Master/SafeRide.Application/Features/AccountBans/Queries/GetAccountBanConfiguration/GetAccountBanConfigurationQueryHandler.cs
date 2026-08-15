using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.AccountBans.DTOs;

namespace SafeRide.Application.Features.AccountBans.Queries.GetAccountBanConfiguration;

public sealed class GetAccountBanConfigurationQueryHandler
    : IRequestHandler<GetAccountBanConfigurationQuery, AccountBanConfigurationResponse>
{
    private readonly IAccountBanManagementService _accountBanManagementService;

    public GetAccountBanConfigurationQueryHandler(
        IAccountBanManagementService accountBanManagementService)
    {
        _accountBanManagementService = accountBanManagementService;
    }

    public Task<AccountBanConfigurationResponse> Handle(
        GetAccountBanConfigurationQuery request,
        CancellationToken cancellationToken)
    {
        return _accountBanManagementService.GetConfigurationAsync(cancellationToken);
    }
}
