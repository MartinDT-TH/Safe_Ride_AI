using System.ComponentModel.DataAnnotations;

namespace SafeRide.Infrastructure.ExternalServices.PayOS;

public sealed class PayOsOptions
{
    public const string SectionName = "PayOS";

    public string BaseUrl { get; init; } = "https://api-merchant.payos.vn";

    [Required]
    public string ClientId { get; init; } = string.Empty;

    [Required]
    public string ApiKey { get; init; } = string.Empty;

    [Required]
    public string ChecksumKey { get; init; } = string.Empty;

    public string ReturnUrl { get; init; } = "saferide://payment/success";

    public string CancelUrl { get; init; } = "saferide://payment/cancel";
}
