using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Drivers.DTOs;

public enum WalletPeriod
{
    Day,
    Week,
    Month
}

public sealed record DriverWalletDto(
    decimal AvailableBalance,
    WalletIncomeSummaryDto Income,
    IReadOnlyList<DriverWalletTransactionDto> RecentTransactions,
    SavedBankAccountDto? SavedBankAccount);

public sealed record SavedBankAccountDto(
    string BankName,
    string BankAccountNumber,
    string BankAccountName);

public sealed record WalletIncomeSummaryDto(
    WalletPeriod Period,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal Total,
    decimal PreviousTotal,
    decimal? ChangePercentage,
    IReadOnlyList<WalletChartPointDto> Chart);

public sealed record WalletChartPointDto(
    DateTime Start,
    string Label,
    decimal Amount);

public sealed record DriverWalletTransactionDto(
    long Id,
    long? TripId,
    WalletTransactionType Type,
    decimal Amount,
    bool IsCredit,
    string Title,
    string? Description,
    DateTime CreatedAt);

public sealed record WithdrawalRequestDto(
    long Id,
    decimal Amount,
    WithdrawalRequestStatus Status,
    DateTime CreatedAt,
    decimal AvailableBalance);
