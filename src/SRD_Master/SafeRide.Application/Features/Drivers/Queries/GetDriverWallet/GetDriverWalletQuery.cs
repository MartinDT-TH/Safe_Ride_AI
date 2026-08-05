using MediatR;
using SafeRide.Application.Features.Drivers.DTOs;

namespace SafeRide.Application.Features.Drivers.Queries.GetDriverWallet;

public sealed record GetDriverWalletQuery(
    Guid DriverId,
    WalletPeriod Period,
    int UtcOffsetMinutes,
    int RecentLimit) : IRequest<DriverWalletDto>;
