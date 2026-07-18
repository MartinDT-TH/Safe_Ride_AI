using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Drivers.DTOs;

namespace SafeRide.Application.Features.Drivers.Commands.RequestWithdrawal;

public sealed class RequestWithdrawalCommandHandler
    : IRequestHandler<RequestWithdrawalCommand, WithdrawalRequestDto>
{
    private readonly IDriverWalletService _walletService;

    public RequestWithdrawalCommandHandler(IDriverWalletService walletService)
    {
        _walletService = walletService;
    }

    public Task<WithdrawalRequestDto> Handle(
        RequestWithdrawalCommand request,
        CancellationToken cancellationToken)
    {
        return _walletService.RequestWithdrawalAsync(
            request.DriverId,
            request.Amount,
            request.BankName,
            request.BankAccountNumber,
            request.BankAccountName,
            cancellationToken);
    }
}
