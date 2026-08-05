using System.Data;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Drivers;
using SafeRide.Application.Features.Drivers.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class DriverWalletService : IDriverWalletService
{
    private readonly ApplicationDbContext _dbContext;

    public DriverWalletService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WithdrawalRequestDto> RequestWithdrawalAsync(
        Guid driverId,
        decimal amount,
        string bankName,
        string bankAccountNumber,
        string bankAccountName,
        CancellationToken cancellationToken)
    {
        if (amount < 10000m)
        {
            throw new DriverWalletException("Số tiền rút tối thiểu là 10.000đ.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var wallet = await _dbContext.DriverWallets
            .SingleOrDefaultAsync(x => x.DriverId == driverId, cancellationToken);
        if (wallet is null)
        {
            throw new DriverWalletException("Ví tài xế chưa được khởi tạo.");
        }

        if (amount > wallet.CurrentBalance)
        {
            throw new DriverWalletException("Số dư khả dụng không đủ.");
        }

        var now = DateTime.UtcNow;
        var request = new WithdrawalRequest
        {
            WalletId = wallet.Id,
            Amount = decimal.Round(amount, 0, MidpointRounding.AwayFromZero),
            BankName = bankName.Trim(),
            BankAccountNumber = bankAccountNumber.Trim(),
            BankAccountName = bankAccountName.Trim(),
            Status = WithdrawalRequestStatus.Pending,
            CreatedAt = now
        };

        _dbContext.WithdrawalRequests.Add(request);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new WithdrawalRequestDto(
            request.Id,
            request.Amount,
            request.Status,
            request.CreatedAt,
            wallet.CurrentBalance);
    }
}
