using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Features.Bookings.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class TripPaymentSettlementService
{
    private const decimal DriverShareRate = 0.70m;
    private readonly ApplicationDbContext _dbContext;

    public TripPaymentSettlementService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SettleSuccessfulQrPaymentAsync(
        Trip trip,
        string? providerReference,
        CancellationToken cancellationToken)
    {
        var payment = trip.Payments
            .OrderByDescending(item => item.PaymentStatus == PaymentStatus.Success)
            .ThenByDescending(item => item.CreatedAt)
            .FirstOrDefault(item => item.PaymentStatus == PaymentStatus.Success);
        if (payment?.PaymentMethod != PaymentMethod.QR)
        {
            return;
        }

        var alreadyCredited = await _dbContext.WalletTransactions.AnyAsync(
            item => item.TripId == trip.Id
                && item.TransactionType == WalletTransactionType.Income,
            cancellationToken);
        if (alreadyCredited)
        {
            return;
        }

        var wallet = await _dbContext.DriverWallets
            .FirstOrDefaultAsync(item => item.DriverId == trip.DriverId, cancellationToken);
        if (wallet is null)
        {
            wallet = new DriverWallet
            {
                DriverId = trip.DriverId,
                CurrentBalance = 0m
            };
            _dbContext.DriverWallets.Add(wallet);
        }

        var originalFare = trip.ActualFare ?? BookingPriceMapper.FromBooking(trip.Booking).OriginalFare;
        var driverShare = Math.Round(
            originalFare * DriverShareRate,
            0,
            MidpointRounding.AwayFromZero);
        wallet.CurrentBalance += driverShare;
        _dbContext.WalletTransactions.Add(new WalletTransaction
        {
            Wallet = wallet,
            TripId = trip.Id,
            TransactionType = WalletTransactionType.Income,
            Amount = driverShare,
            Description = string.IsNullOrWhiteSpace(providerReference)
                ? "SafeRide QR trip payout"
                : $"SafeRide QR trip payout ({providerReference})",
            CreatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
