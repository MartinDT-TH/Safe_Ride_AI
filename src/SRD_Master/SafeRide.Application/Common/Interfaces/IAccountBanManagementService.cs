using SafeRide.Application.Features.AccountBans.DTOs;

namespace SafeRide.Application.Common.Interfaces;

public interface IAccountBanManagementService
{
    Task<AccountBanConfigurationResponse> GetConfigurationAsync(
        CancellationToken cancellationToken);

    Task<AccountBanConfigurationResponse> UpdateConfigurationAsync(
        int negativeFeedbackThreshold,
        int negativeRatingMaxScore,
        int temporaryBanDurationDays,
        int maximumTemporaryBans,
        bool isEnabled,
        Guid updatedByUserId,
        CancellationToken cancellationToken);
}
