using MediatR;
using SafeRide.Application.Features.Drivers.DTOs;

namespace SafeRide.Application.Features.Drivers.Commands.RequestWithdrawal;

public sealed record RequestWithdrawalCommand(
    Guid DriverId,
    decimal Amount,
    string BankName,
    string BankAccountNumber,
    string BankAccountName) : IRequest<WithdrawalRequestDto>;
