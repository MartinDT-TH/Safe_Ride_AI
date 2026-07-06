using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.Bookings.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.ExternalServices.PayOS;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class PayOsPaymentService : IPaymentService
{
    private const decimal DriverShareRate = 0.70m;
    private const decimal PlatformShareRate = 0.30m;
    private const string Currency = "VND";

    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _dbContext;
    private readonly ITripStatusService _tripStatusService;
    private readonly IRealtimeNotificationService _realtimeNotificationService;
    private readonly PayOsOptions _options;

    public PayOsPaymentService(
        HttpClient httpClient,
        ApplicationDbContext dbContext,
        ITripStatusService tripStatusService,
        IRealtimeNotificationService realtimeNotificationService,
        IOptions<PayOsOptions> options)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _tripStatusService = tripStatusService;
        _realtimeNotificationService = realtimeNotificationService;
        _options = options.Value;
    }

    public async Task<QrPaymentResult> CreateQrPaymentAsync(
        Guid customerId,
        long tripId,
        string? returnUrl,
        string? cancelUrl,
        CancellationToken cancellationToken)
    {
        EnsurePayOsConfigured();

        var trip = await GetCustomerPayableTripAsync(customerId, tripId, cancellationToken);
        return await CreateQrPaymentForTripAsync(
            trip,
            returnUrl,
            cancelUrl,
            cancellationToken);
    }

    public async Task<QrPaymentResult> CreateDriverQrPaymentAsync(
        Guid driverId,
        long tripId,
        string? returnUrl,
        string? cancelUrl,
        CancellationToken cancellationToken)
    {
        EnsurePayOsConfigured();

        var trip = await GetDriverPayableTripAsync(driverId, tripId, cancellationToken);
        return await CreateQrPaymentForTripAsync(
            trip,
            returnUrl,
            cancelUrl,
            cancellationToken);
    }

    private async Task<QrPaymentResult> CreateQrPaymentForTripAsync(
        Trip trip,
        string? returnUrl,
        string? cancelUrl,
        CancellationToken cancellationToken)
    {
        var price = BookingPriceMapper.FromBooking(trip.Booking);
        var amount = ToVnd(price.FinalFare);
        if (amount <= 0)
        {
            throw new BookingException(
                "payment.invalid_amount",
                "Số tiền thanh toán không hợp lệ.",
                StatusCodes.Status409Conflict);
        }

        var existingSuccess = trip.Payments
            .FirstOrDefault(x => x.PaymentStatus == PaymentStatus.Success);
        if (existingSuccess is not null)
        {
            return new QrPaymentResult(
                trip.Id,
                existingSuccess.Id,
                existingSuccess.TransactionReference ?? existingSuccess.Id.ToString(CultureInfo.InvariantCulture),
                existingSuccess.Amount,
                existingSuccess.Currency,
                existingSuccess.PaymentStatus,
                trip.TripStatus,
                null,
                null,
                existingSuccess.CreatedAt,
                BuildPaymentMessage(trip.TripStatus, existingSuccess.PaymentStatus));
        }

        var payment = trip.Payments.FirstOrDefault();
        if (payment is not null)
        {
            payment.PaymentMethod = PaymentMethod.QR;
            payment.TransactionReference = BuildOrderCode(trip.Id);
            payment.Amount = amount;
            payment.Currency = Currency;
            payment.PaymentStatus = PaymentStatus.Pending;
            payment.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            payment = new Payment
            {
                TripId = trip.Id,
                PaymentMethod = PaymentMethod.QR,
                TransactionReference = BuildOrderCode(trip.Id),
                Amount = amount,
                Currency = Currency,
                PaymentStatus = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Payments.Add(payment);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var orderCode = long.Parse(payment.TransactionReference!, CultureInfo.InvariantCulture);
        var description = BuildPaymentDescription(trip.Id);
        var effectiveReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
            ? _options.ReturnUrl
            : returnUrl;
        var effectiveCancelUrl = string.IsNullOrWhiteSpace(cancelUrl)
            ? _options.CancelUrl
            : cancelUrl;

        var signature = SignCreatePayment(
            amount,
            effectiveCancelUrl,
            description,
            orderCode,
            effectiveReturnUrl);

        var request = new PayOsCreatePaymentRequest(
            orderCode,
            (int)amount,
            description,
            effectiveReturnUrl,
            effectiveCancelUrl,
            signature);

        var response = await _httpClient.PostAsJsonAsync(
            "/v2/payment-requests",
            request,
            cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var payload = string.IsNullOrWhiteSpace(responseBody)
            ? null
            : JsonSerializer.Deserialize<PayOsCreatePaymentResponse>(
                responseBody,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

        if (!response.IsSuccessStatusCode || payload?.Data is null || payload.Code != "00")
        {
            var payOsMessage = payload is null
                ? responseBody
                : $"PayOS {payload.Code}: {payload.Desc}";
            throw new BookingException(
                "payment.payos_create_failed",
                string.IsNullOrWhiteSpace(payOsMessage)
                    ? "Không thể tạo mã thanh toán PayOS."
                    : payOsMessage,
                StatusCodes.Status502BadGateway);
        }

        payment.Amount = payload.Data.Amount > 0 ? payload.Data.Amount : amount;
        payment.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new QrPaymentResult(
            trip.Id,
            payment.Id,
            orderCode.ToString(CultureInfo.InvariantCulture),
            payment.Amount,
            payment.Currency,
            payment.PaymentStatus,
            trip.TripStatus,
            payload.Data.QrCode,
            payload.Data.CheckoutUrl,
            payment.CreatedAt,
            BuildPaymentMessage(trip.TripStatus, payment.PaymentStatus));
    }

    public async Task<PaymentStatusResult> GetTripPaymentStatusAsync(
        Guid customerId,
        long tripId,
        CancellationToken cancellationToken)
    {
        var trip = await GetCustomerPayableTripAsync(customerId, tripId, cancellationToken);
        var pendingQr = trip.Payments
            .Where(x => x.PaymentMethod == PaymentMethod.QR
                && x.PaymentStatus == PaymentStatus.Pending
                && x.TransactionReference != null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        if (pendingQr is not null && IsPayOsConfigured())
        {
            await RefreshPayOsPaymentAsync(trip, pendingQr, cancellationToken);
        }

        await _dbContext.Entry(trip).Collection(x => x.Payments).LoadAsync(cancellationToken);
        return BuildStatusResult(trip);
    }

    public async Task<PaymentStatusResult> GetDriverTripPaymentStatusAsync(
        Guid driverId,
        long tripId,
        CancellationToken cancellationToken)
    {
        var trip = await GetDriverPayableTripAsync(driverId, tripId, cancellationToken);
        var pendingQr = trip.Payments
            .Where(x => x.PaymentMethod == PaymentMethod.QR
                && x.PaymentStatus == PaymentStatus.Pending
                && x.TransactionReference != null)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        if (pendingQr is not null && IsPayOsConfigured())
        {
            await RefreshPayOsPaymentAsync(trip, pendingQr, cancellationToken);
        }

        await _dbContext.Entry(trip).Collection(x => x.Payments).LoadAsync(cancellationToken);
        return BuildStatusResult(trip);
    }

    public async Task<PaymentStatusResult> ConfirmCashPaymentAsync(
        Guid driverId,
        long tripId,
        CancellationToken cancellationToken)
    {
        var trip = await GetDriverPayableTripAsync(driverId, tripId, cancellationToken);
        if (trip.Payments.Any(x => x.PaymentStatus == PaymentStatus.Success))
        {
            await CompleteTripAfterPaymentAsync(trip, cancellationToken);
            return BuildStatusResult(trip);
        }

        var price = BookingPriceMapper.FromBooking(trip.Booking);
        var finalFare = ToVnd(price.FinalFare);
        var platformShare = ToVnd(price.OriginalFare * PlatformShareRate);
        var wallet = await GetDriverWalletAsync(trip.DriverId, cancellationToken);

        if (wallet.CurrentBalance < platformShare)
        {
            throw new BookingException(
                "payment.insufficient_driver_wallet",
                $"Ví tài xế cần tối thiểu {platformShare:N0}đ để chọn trả tiền mặt.",
                StatusCodes.Status409Conflict);
        }

        wallet.CurrentBalance -= platformShare;
        var payment = trip.Payments.FirstOrDefault();
        if (payment is not null)
        {
            payment.PaymentMethod = PaymentMethod.CASH;
            payment.TransactionReference = null;
            payment.Amount = finalFare;
            payment.Currency = Currency;
            payment.PaymentStatus = PaymentStatus.Success;
            payment.PaidAt = DateTime.UtcNow;
            payment.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            payment = new Payment
            {
                TripId = trip.Id,
                PaymentMethod = PaymentMethod.CASH,
                TransactionReference = null,
                Amount = finalFare,
                Currency = Currency,
                PaymentStatus = PaymentStatus.Success,
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.Payments.Add(payment);
        }
        _dbContext.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id,
            TripId = trip.Id,
            TransactionType = WalletTransactionType.Penalty,
            Amount = platformShare,
            Description = "SafeRide commission for cash trip",
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await CompleteTripAfterPaymentAsync(trip, cancellationToken);
        await PublishTripPaymentSucceededAsync(trip, payment, cancellationToken);
        return BuildStatusResult(trip);
    }

    public async Task<PaymentStatusResult> ConfirmDemoQrPaymentAsync(
        DemoQrPaymentWebhookRequest request,
        CancellationToken cancellationToken)
    {
        var trip = await GetDemoQrPayableTripAsync(request.TripId, cancellationToken);
        if (trip.Payments.Any(x => x.PaymentStatus == PaymentStatus.Success))
        {
            await CompleteTripAfterPaymentAsync(trip, cancellationToken);
            return BuildStatusResult(trip);
        }

        var price = BookingPriceMapper.FromBooking(trip.Booking);
        var amount = request.Amount.HasValue && request.Amount.Value > 0
            ? ToVnd(request.Amount.Value)
            : ToVnd(price.FinalFare);

        var payment = FindPaymentForDemoWebhook(trip, request.OrderCode);
        if (payment is null)
        {
            payment = new Payment
            {
                TripId = trip.Id,
                PaymentMethod = PaymentMethod.QR,
                TransactionReference = string.IsNullOrWhiteSpace(request.OrderCode)
                    ? BuildOrderCode(trip.Id)
                    : request.OrderCode,
                Amount = amount,
                Currency = Currency,
                PaymentStatus = PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Payments.Add(payment);
        }
        else
        {
            payment.PaymentMethod = PaymentMethod.QR;
            payment.TransactionReference = string.IsNullOrWhiteSpace(request.OrderCode)
                ? payment.TransactionReference ?? BuildOrderCode(trip.Id)
                : request.OrderCode;
            payment.Amount = amount;
            payment.Currency = Currency;
            payment.UpdatedAt = DateTime.UtcNow;
        }

        await MarkQrPaymentSuccessAsync(
            trip,
            payment,
            amount,
            "mock-demo",
            cancellationToken);

        return BuildStatusResult(trip);
    }

    public async Task HandlePayOsWebhookAsync(
        PayOsWebhookRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Data is null)
        {
            return;
        }

        if (!VerifyWebhookSignature(request))
        {
            throw new BookingException(
                "payment.invalid_webhook_signature",
                "PayOS webhook signature is invalid.",
                StatusCodes.Status400BadRequest);
        }

        var payment = await _dbContext.Payments
            .Include(x => x.Trip)
                .ThenInclude(x => x.Booking)
                    .ThenInclude(x => x.BookingPromotions)
                        .ThenInclude(x => x.Promotion)
            .Include(x => x.Trip)
                .ThenInclude(x => x.WalletTransactions)
            .FirstOrDefaultAsync(
                x => x.TransactionReference == request.Data.OrderCode.ToString(CultureInfo.InvariantCulture),
                cancellationToken);

        if (payment is null
            || payment.PaymentStatus == PaymentStatus.Success
            || payment.PaymentStatus == PaymentStatus.Cancelled)
        {
            return;
        }

        if (request.Success && (request.Code == "00" || request.Data.Code == "00"))
        {
            await MarkQrPaymentSuccessAsync(
                payment.Trip,
                payment,
                request.Data.Amount,
                request.Data.Reference,
                cancellationToken);
            return;
        }

        payment.PaymentStatus = PaymentStatus.Failed;
        payment.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task RefreshPayOsPaymentAsync(
        Trip trip,
        Payment payment,
        CancellationToken cancellationToken)
    {
        var orderCode = payment.TransactionReference;
        var response = await _httpClient.GetFromJsonAsync<PayOsGetPaymentResponse>(
            $"/v2/payment-requests/{orderCode}",
            cancellationToken);
        var data = response?.Data;
        if (response?.Code != "00" || data is null)
        {
            return;
        }

        if (string.Equals(data.Status, "PAID", StringComparison.OrdinalIgnoreCase))
        {
            await MarkQrPaymentSuccessAsync(
                trip,
                payment,
                data.AmountPaid ?? data.Amount,
                data.Id,
                cancellationToken);
        }
    }

    private async Task MarkQrPaymentSuccessAsync(
        Trip trip,
        Payment payment,
        decimal paidAmount,
        string? providerReference,
        CancellationToken cancellationToken)
    {
        if (payment.PaymentStatus == PaymentStatus.Success)
        {
            await CompleteTripAfterPaymentAsync(trip, cancellationToken);
            return;
        }

        var price = BookingPriceMapper.FromBooking(trip.Booking);
        var driverShare = ToVnd(price.OriginalFare * DriverShareRate);
        var wallet = await GetDriverWalletAsync(trip.DriverId, cancellationToken);
        var alreadyCredited = await _dbContext.WalletTransactions.AnyAsync(
            x => x.TripId == trip.Id
                && x.WalletId == wallet.Id
                && x.TransactionType == WalletTransactionType.Income,
            cancellationToken);

        payment.PaymentStatus = PaymentStatus.Success;
        payment.Amount = paidAmount > 0 ? ToVnd(paidAmount) : payment.Amount;
        payment.PaidAt = DateTime.UtcNow;
        payment.UpdatedAt = DateTime.UtcNow;

        if (!alreadyCredited)
        {
            wallet.CurrentBalance += driverShare;
            _dbContext.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = wallet.Id,
                TripId = trip.Id,
                TransactionType = WalletTransactionType.Income,
                Amount = driverShare,
                Description = string.IsNullOrWhiteSpace(providerReference)
                    ? "SafeRide QR trip payout"
                    : $"SafeRide QR trip payout ({providerReference})",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await CompleteTripAfterPaymentAsync(trip, cancellationToken);
        await PublishTripPaymentSucceededAsync(trip, payment, cancellationToken);
    }

    private async Task<Trip> GetCustomerPayableTripAsync(
        Guid customerId,
        long tripId,
        CancellationToken cancellationToken)
    {
        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .Include(x => x.Payments)
            .Include(x => x.WalletTransactions)
            .FirstOrDefaultAsync(
                x => x.Id == tripId && x.Booking.CustomerId == customerId,
                cancellationToken);

        if (trip is null)
        {
            throw new BookingException(
                "trip.not_found",
                "Không tìm thấy chuyến đi.",
                StatusCodes.Status404NotFound);
        }

        if (!IsPaymentVisibleStatus(trip.TripStatus))
        {
            throw new BookingException(
                "payment.trip_not_waiting_payment",
                "Vui lòng thanh toán cho tài xế sau khi đã xác nhận trả xe.",
                StatusCodes.Status409Conflict);
        }

        return trip;
    }

    private async Task<Trip> GetDriverPayableTripAsync(
        Guid driverId,
        long tripId,
        CancellationToken cancellationToken)
    {
        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .Include(x => x.Payments)
            .Include(x => x.WalletTransactions)
            .FirstOrDefaultAsync(
                x => x.Id == tripId && x.DriverId == driverId,
                cancellationToken);

        if (trip is null)
        {
            throw new BookingException(
                "trip.not_found",
                "Không tìm thấy chuyến đi.",
                StatusCodes.Status404NotFound);
        }

        if (!IsPaymentVisibleStatus(trip.TripStatus))
        {
            throw new BookingException(
                "payment.trip_not_waiting_payment",
                "Chỉ xác nhận thanh toán sau khi khách đã xác nhận trả xe.",
                StatusCodes.Status409Conflict);
        }

        return trip;
    }

    private async Task<Trip> GetDemoQrPayableTripAsync(
        long tripId,
        CancellationToken cancellationToken)
    {
        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .Include(x => x.Payments)
            .Include(x => x.WalletTransactions)
            .FirstOrDefaultAsync(x => x.Id == tripId, cancellationToken);

        if (trip is null)
        {
            throw new BookingException(
                "trip.not_found",
                "Không tìm thấy chuyến đi.",
                StatusCodes.Status404NotFound);
        }

        if (!IsPaymentVisibleStatus(trip.TripStatus))
        {
            throw new BookingException(
                "payment.trip_not_waiting_payment",
                "Chuyến đi chưa sẵn sàng để xác nhận thanh toán.",
                StatusCodes.Status409Conflict);
        }

        return trip;
    }

    private async Task<DriverWallet> GetDriverWalletAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var wallet = await _dbContext.DriverWallets
            .FirstOrDefaultAsync(x => x.DriverId == driverId, cancellationToken);
        if (wallet is not null)
        {
            return wallet;
        }

        wallet = new DriverWallet
        {
            DriverId = driverId,
            CurrentBalance = 0m
        };
        _dbContext.DriverWallets.Add(wallet);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return wallet;
    }

    private static Payment? FindPaymentForDemoWebhook(Trip trip, string? orderCode)
    {
        if (!string.IsNullOrWhiteSpace(orderCode))
        {
            var byOrderCode = trip.Payments
                .FirstOrDefault(x => x.TransactionReference == orderCode);
            if (byOrderCode is not null)
            {
                return byOrderCode;
            }
        }

        return trip.Payments
            .OrderByDescending(x => x.PaymentStatus == PaymentStatus.Pending)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefault();
    }

    private async Task CompleteTripAfterPaymentAsync(
        Trip trip,
        CancellationToken cancellationToken)
    {
        if (trip.TripStatus == TripStatus.COMPLETED)
        {
            return;
        }

        await _tripStatusService.CompleteTripAsync(
            trip.DriverId,
            trip.Id,
            cancellationToken);
    }

    private Task PublishTripPaymentSucceededAsync(
        Trip trip,
        Payment payment,
        CancellationToken cancellationToken)
    {
        return _realtimeNotificationService.PublishTripPaymentSucceededAsync(
            new TripPaymentSucceededEvent(
                trip.Id,
                trip.BookingId,
                trip.Booking.CustomerId,
                trip.DriverId,
                payment.Id,
                payment.PaymentMethod,
                payment.PaymentStatus,
                payment.Amount,
                payment.Currency,
                trip.TripStatus,
                payment.PaidAt ?? DateTime.UtcNow,
                "Thanh toán đã hoàn tất.",
                trip.Booking.BookingStatus),
            cancellationToken);
    }

    private PaymentStatusResult BuildStatusResult(Trip trip)
    {
        var price = BookingPriceMapper.FromBooking(trip.Booking);
        var originalFare = ToVnd(price.OriginalFare);
        var finalFare = ToVnd(price.FinalFare);
        var driverShare = ToVnd(originalFare * DriverShareRate);
        var platformShare = ToVnd(originalFare * PlatformShareRate);
        var payment = trip.Payments
            .OrderByDescending(x => x.PaymentStatus == PaymentStatus.Success)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefault();

        return new PaymentStatusResult(
            trip.Id,
            payment?.Id,
            payment?.PaymentMethod,
            payment?.PaymentStatus ?? PaymentStatus.Pending,
            payment?.Amount ?? finalFare,
            originalFare,
            finalFare,
            driverShare,
            platformShare,
            Currency,
            payment?.PaidAt,
            trip.TripStatus,
            BuildPaymentMessage(
                trip.TripStatus,
                payment?.PaymentStatus ?? PaymentStatus.Pending));
    }

    private static bool IsPaymentVisibleStatus(TripStatus status)
        => status is TripStatus.WAITING_PAYMENT or TripStatus.COMPLETED;

    private static string BuildPaymentMessage(
        TripStatus tripStatus,
        PaymentStatus paymentStatus)
    {
        if (paymentStatus == PaymentStatus.Success || tripStatus == TripStatus.COMPLETED)
        {
            return "Thanh toán đã hoàn tất.";
        }

        return "Vui lòng thanh toán cho tài xế để hoàn tất chuyến đi.";
    }

    private string SignCreatePayment(
        decimal amount,
        string cancelUrl,
        string description,
        long orderCode,
        string returnUrl)
    {
        var rawData =
            $"amount={(int)amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";
        return HmacSha256(rawData, _options.ChecksumKey);
    }

    private bool VerifyWebhookSignature(PayOsWebhookRequest request)
    {
        if (request.Data is null)
        {
            return false;
        }

        var data = request.Data;
        var values = new SortedDictionary<string, string?>
        {
            ["accountNumber"] = data.AccountNumber,
            ["amount"] = ((long)ToVnd(data.Amount)).ToString(CultureInfo.InvariantCulture),
            ["code"] = data.Code,
            ["counterAccountBankId"] = data.CounterAccountBankId,
            ["counterAccountBankName"] = data.CounterAccountBankName,
            ["counterAccountName"] = data.CounterAccountName,
            ["counterAccountNumber"] = data.CounterAccountNumber,
            ["currency"] = data.Currency,
            ["desc"] = data.Desc,
            ["description"] = data.Description,
            ["orderCode"] = data.OrderCode.ToString(CultureInfo.InvariantCulture),
            ["paymentLinkId"] = data.PaymentLinkId,
            ["reference"] = data.Reference,
            ["transactionDateTime"] = data.TransactionDateTime,
            ["virtualAccountName"] = data.VirtualAccountName,
            ["virtualAccountNumber"] = data.VirtualAccountNumber
        };

        var rawData = string.Join(
            '&',
            values
                .Where(x => x.Value is not null)
                .Select(x => $"{x.Key}={x.Value}"));
        var expected = HmacSha256(rawData, _options.ChecksumKey);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(request.Signature));
    }

    private static string HmacSha256(string rawData, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static decimal ToVnd(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);

    private static string BuildOrderCode(long tripId)
    {
        var suffix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 1_000_000;
        return $"{tripId}{suffix:000000}";
    }

    private static string BuildPaymentDescription(long tripId)
    {
        return $"SRD{tripId % 1_000_000:000000}";
    }

    private bool IsPayOsConfigured()
    {
        return !string.IsNullOrWhiteSpace(_options.ClientId)
            && !string.IsNullOrWhiteSpace(_options.ApiKey)
            && !string.IsNullOrWhiteSpace(_options.ChecksumKey);
    }

    private void EnsurePayOsConfigured()
    {
        if (!IsPayOsConfigured())
        {
            throw new BookingException(
                "payment.payos_not_configured",
                "PayOS chưa được cấu hình đầy đủ. Vui lòng bổ sung ClientId, ApiKey và ChecksumKey.",
                StatusCodes.Status503ServiceUnavailable);
        }
    }
}
