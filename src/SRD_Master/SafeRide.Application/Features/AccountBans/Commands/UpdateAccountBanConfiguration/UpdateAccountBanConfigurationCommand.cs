using MediatR;
using SafeRide.Application.Features.AccountBans.DTOs;

namespace SafeRide.Application.Features.AccountBans.Commands.UpdateAccountBanConfiguration;

public sealed record UpdateAccountBanConfigurationCommand(
    int NegativeFeedbackThreshold,
    int NegativeRatingMaxScore,
    int TemporaryBanDurationDays,
    int MaximumTemporaryBans,
    bool IsEnabled,
    Guid UpdatedByUserId) : IRequest<AccountBanConfigurationResponse>;
