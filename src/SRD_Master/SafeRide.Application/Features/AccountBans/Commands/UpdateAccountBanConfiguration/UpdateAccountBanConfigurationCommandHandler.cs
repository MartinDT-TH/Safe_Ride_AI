using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.AccountBans.DTOs;

namespace SafeRide.Application.Features.AccountBans.Commands.UpdateAccountBanConfiguration;

public sealed class UpdateAccountBanConfigurationCommandHandler
    : IRequestHandler<UpdateAccountBanConfigurationCommand, AccountBanConfigurationResponse>
{
    private readonly IAccountBanManagementService _accountBanManagementService;

    public UpdateAccountBanConfigurationCommandHandler(
        IAccountBanManagementService accountBanManagementService)
    {
        _accountBanManagementService = accountBanManagementService;
    }

    public Task<AccountBanConfigurationResponse> Handle(
        UpdateAccountBanConfigurationCommand request,
        CancellationToken cancellationToken)
    {
        return _accountBanManagementService.UpdateConfigurationAsync(
            request.NegativeFeedbackThreshold,
            request.NegativeRatingMaxScore,
            request.TemporaryBanDurationDays,
            request.MaximumTemporaryBans,
            request.IsEnabled,
            request.UpdatedByUserId,
            cancellationToken);
    }
}
