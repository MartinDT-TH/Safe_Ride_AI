using System.ComponentModel.DataAnnotations;

namespace SafeRide.Contracts.Requests.Drivers;

public sealed class CreateWithdrawalRequest
{
    [Range(10000, 100000000)]
    public decimal Amount { get; init; }

    [Required, StringLength(100)]
    public string BankName { get; init; } = string.Empty;

    [Required, StringLength(50, MinimumLength = 4)]
    [RegularExpression("^[0-9]+$")]
    public string BankAccountNumber { get; init; } = string.Empty;

    [Required, StringLength(150)]
    public string BankAccountName { get; init; } = string.Empty;
}
