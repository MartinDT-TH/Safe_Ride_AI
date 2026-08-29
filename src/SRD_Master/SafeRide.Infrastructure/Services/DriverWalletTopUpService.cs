using System.Data;
using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.ExternalServices.PayOS;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class DriverWalletTopUpService(
    HttpClient httpClient,
    ApplicationDbContext dbContext,
    IOptions<PayOsOptions> options) : IDriverWalletTopUpService
{
    private const decimal MinimumAmount = 10_000m;
    private const decimal MaximumAmount = 100_000_000m;
    private readonly PayOsOptions _options = options.Value;

    public async Task<WalletTopUpResult> CreateAsync(
        Guid driverId,
        decimal amount,
        string? returnUrl,
        string? cancelUrl,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        amount = Math.Round(amount, 0, MidpointRounding.AwayFromZero);
        if (amount < MinimumAmount || amount > MaximumAmount)
        {
            throw new BookingException("wallet.top_up.invalid_amount", "Số tiền nạp phải từ 10.000đ đến 100.000.000đ.", StatusCodes.Status400BadRequest);
        }

        var wallet = await dbContext.DriverWallets.SingleOrDefaultAsync(x => x.DriverId == driverId, cancellationToken);
        if (wallet is null)
        {
            var driverExists = await dbContext.DriverProfiles.AnyAsync(x => x.DriverId == driverId, cancellationToken);
            if (!driverExists)
                throw new BookingException("wallet.driver_not_found", "Không tìm thấy hồ sơ tài xế.", StatusCodes.Status404NotFound);
            wallet = new DriverWallet { DriverId = driverId };
            dbContext.DriverWallets.Add(wallet);
        }

        var orderCode = checked(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000 + Random.Shared.Next(1000));
        var topUp = new DriverWalletTopUp { Wallet = wallet, OrderCode = orderCode, Amount = amount };
        dbContext.DriverWalletTopUps.Add(topUp);
        await dbContext.SaveChangesAsync(cancellationToken);

        var effectiveReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? _options.ReturnUrl : returnUrl;
        var effectiveCancelUrl = string.IsNullOrWhiteSpace(cancelUrl) ? _options.CancelUrl : cancelUrl;
        var description = $"NAPVI{topUp.Id % 10_000_000:0000000}";
        var signature = SignCreatePayment(amount, effectiveCancelUrl, description, orderCode, effectiveReturnUrl);
        var response = await httpClient.PostAsJsonAsync("/v2/payment-requests",
            new PayOsCreatePaymentRequest(orderCode, (int)amount, description, effectiveReturnUrl, effectiveCancelUrl, signature), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var payload = string.IsNullOrWhiteSpace(body) ? null : JsonSerializer.Deserialize<PayOsCreatePaymentResponse>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (!response.IsSuccessStatusCode || payload?.Code != "00" || payload.Data is null)
        {
            topUp.Status = PaymentStatus.Failed;
            topUp.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new BookingException("wallet.top_up.payos_create_failed", "Không thể tạo mã QR nạp tiền PayOS.", StatusCodes.Status502BadGateway);
        }

        topUp.PaymentLinkId = payload.Data.Id;
        topUp.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResult(topUp, payload.Data.QrCode, payload.Data.CheckoutUrl);
    }

    public async Task<WalletTopUpResult> GetStatusAsync(Guid driverId, long topUpId, CancellationToken cancellationToken)
    {
        var topUp = await dbContext.DriverWalletTopUps.Include(x => x.Wallet)
            .SingleOrDefaultAsync(x => x.Id == topUpId && x.Wallet.DriverId == driverId, cancellationToken)
            ?? throw new BookingException("wallet.top_up.not_found", "Không tìm thấy giao dịch nạp tiền.", StatusCodes.Status404NotFound);
        return ToResult(topUp, null, null);
    }

    public async Task<bool> TryHandleWebhookAsync(PayOsWebhookRequest request, CancellationToken cancellationToken)
    {
        if (request.Data is null) return false;
        var exists = await dbContext.DriverWalletTopUps.AnyAsync(x => x.OrderCode == request.Data.OrderCode, cancellationToken);
        if (!exists) return false;
        if (!VerifyWebhookSignature(request))
            throw new BookingException("payment.invalid_webhook_signature", "PayOS webhook signature is invalid.", StatusCodes.Status400BadRequest);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var topUp = await dbContext.DriverWalletTopUps.Include(x => x.Wallet)
            .SingleAsync(x => x.OrderCode == request.Data.OrderCode, cancellationToken);
        if (topUp.Status == PaymentStatus.Success)
        {
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        if (!request.Success || (request.Code != "00" && request.Data.Code != "00"))
        {
            topUp.Status = PaymentStatus.Failed;
            topUp.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        if (Math.Round(request.Data.Amount, 0) != topUp.Amount)
            throw new BookingException("wallet.top_up.amount_mismatch", "Số tiền PayOS xác nhận không khớp.", StatusCodes.Status409Conflict);

        var now = DateTime.UtcNow;
        topUp.Wallet.CurrentBalance += topUp.Amount;
        topUp.Status = PaymentStatus.Success;
        topUp.PaidAt = now;
        topUp.UpdatedAt = now;
        topUp.ProviderReference = request.Data.Reference;
        dbContext.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = topUp.WalletId,
            TransactionType = WalletTransactionType.TopUp,
            Amount = topUp.Amount,
            Description = $"Nạp tiền qua PayOS - {topUp.OrderCode}",
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private WalletTopUpResult ToResult(DriverWalletTopUp topUp, string? qrCode, string? checkoutUrl) =>
        new(topUp.Id, topUp.OrderCode, topUp.Amount, topUp.Status, qrCode, checkoutUrl,
            topUp.Status == PaymentStatus.Success ? topUp.Wallet.CurrentBalance : null,
            topUp.CreatedAt, topUp.PaidAt,
            topUp.Status == PaymentStatus.Success ? "Nạp tiền thành công." : "Đang chờ PayOS xác nhận thanh toán.");

    private string SignCreatePayment(decimal amount, string cancelUrl, string description, long orderCode, string returnUrl) =>
        HmacSha256($"amount={(int)amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}");

    private bool VerifyWebhookSignature(PayOsWebhookRequest request)
    {
        var d = request.Data!;
        var values = new SortedDictionary<string, string?>
        {
            ["accountNumber"] = d.AccountNumber, ["amount"] = ((long)Math.Round(d.Amount, 0)).ToString(CultureInfo.InvariantCulture),
            ["code"] = d.Code, ["counterAccountBankId"] = d.CounterAccountBankId, ["counterAccountBankName"] = d.CounterAccountBankName,
            ["counterAccountName"] = d.CounterAccountName, ["counterAccountNumber"] = d.CounterAccountNumber, ["currency"] = d.Currency,
            ["desc"] = d.Desc, ["description"] = d.Description, ["orderCode"] = d.OrderCode.ToString(CultureInfo.InvariantCulture),
            ["paymentLinkId"] = d.PaymentLinkId, ["reference"] = d.Reference, ["transactionDateTime"] = d.TransactionDateTime,
            ["virtualAccountName"] = d.VirtualAccountName, ["virtualAccountNumber"] = d.VirtualAccountNumber
        };
        var expected = HmacSha256(string.Join('&', values.Where(x => x.Value is not null).Select(x => $"{x.Key}={x.Value}")));
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(request.Signature));
    }

    private string HmacSha256(string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.ChecksumKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ApiKey) || string.IsNullOrWhiteSpace(_options.ChecksumKey))
            throw new BookingException("payment.payos_not_configured", "PayOS chưa được cấu hình đầy đủ.", StatusCodes.Status503ServiceUnavailable);
    }
}
