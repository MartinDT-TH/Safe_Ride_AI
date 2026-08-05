using SafeRide.Application.Features.Drivers.DTOs;

namespace SafeRide.Application.Common.Interfaces;

public interface IDriverWalletService
{
    Task<WithdrawalRequestDto> RequestWithdrawalAsync(
        Guid driverId,
        decimal amount,
        string bankName,
        string bankAccountNumber,
        string bankAccountName,
        CancellationToken cancellationToken);
}
