using MediatR;
using SafeRide.Application.Features.AccountBans.DTOs;

namespace SafeRide.Application.Features.AccountBans.Queries.GetAccountBanConfiguration;

public sealed record GetAccountBanConfigurationQuery()
    : IRequest<AccountBanConfigurationResponse>;
