using System.Text.Json.Serialization;

namespace SafeRide.Infrastructure.ExternalServices.PayOS;

internal sealed record PayOsCreatePaymentRequest(
    [property: JsonPropertyName("orderCode")] long OrderCode,
    [property: JsonPropertyName("amount")] int Amount,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("returnUrl")] string ReturnUrl,
    [property: JsonPropertyName("cancelUrl")] string CancelUrl,
    [property: JsonPropertyName("signature")] string Signature);

internal sealed record PayOsCreatePaymentResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("desc")] string Desc,
    [property: JsonPropertyName("data")] PayOsPaymentData? Data);

internal sealed record PayOsPaymentData(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("orderCode")] long OrderCode,
    [property: JsonPropertyName("amount")] int Amount,
    [property: JsonPropertyName("amountPaid")] int? AmountPaid,
    [property: JsonPropertyName("amountRemaining")] int? AmountRemaining,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("checkoutUrl")] string? CheckoutUrl,
    [property: JsonPropertyName("qrCode")] string? QrCode);

internal sealed record PayOsGetPaymentResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("desc")] string Desc,
    [property: JsonPropertyName("data")] PayOsPaymentData? Data);
