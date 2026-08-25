using System.Globalization;
using System.Data;
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
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.ExternalServices.PayOS;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class PayOsPaymentService : IPaymentService
{
    private const string Currency = "VND";

    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _dbContext;
    private readonly ITripStatusService _tripStatusService;
    private readonly IRealtimeNotificationService _realtimeNotificationService;
    private readonly TripPaymentSettlementService _tripPaymentSettlementService;
    private readonly ITripFinancialSettlementService _financialSettlementService;
    private readonly IRiskProtectionPolicyProvider _riskProtectionPolicyProvider;
    private readonly ITripCommissionCalculator _commissionCalculator;
    private readonly ISafetyPaymentReconciliationService _safetyPaymentReconciliationService;
    private readonly PayOsOptions _options;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public PayOsPaymentService(
        HttpClient httpClient,
        ApplicationDbContext dbContext,
        ITripStatusService tripStatusService,
        IRealtimeNotificationService realtimeNotificationService,
        TripPaymentSettlementService tripPaymentSettlementService,
        ITripFinancialSettlementService financialSettlementService,
        IRiskProtectionPolicyProvider riskProtectionPolicyProvider,
        ITripCommissionCalculator commissionCalculator,
        IOptions<PayOsOptions> options)
        : this(
            httpClient, dbContext, tripStatusService, realtimeNotificationService,
            tripPaymentSettlementService, financialSettlementService,
            riskProtectionPolicyProvider, commissionCalculator,
            new SafetyPaymentReconciliationService(
                dbContext, financialSettlementService, new SystemDateTimeProvider()),
            options)
    {
    }

    public PayOsPaymentService(
        HttpClient httpClient,
        ApplicationDbContext dbContext,
        ITripStatusService tripStatusService,
        IRealtimeNotificationService realtimeNotificationService,
        TripPaymentSettlementService tripPaymentSettlementService,
        ITripFinancialSettlementService financialSettlementService,
        IRiskProtectionPolicyProvider riskProtectionPolicyProvider,
        ITripCommissionCalculator commissionCalculator,
        ISafetyPaymentReconciliationService safetyPaymentReconciliationService,
        IOptions<PayOsOptions> options)
    {
        _httpClient = httpClient;
        _dbContext = dbContext;
        _tripStatusService = tripStatusService;
        _realtimeNotificationService = realtimeNotificationService;
        _tripPaymentSettlementService = tripPaymentSettlementService;
        _financialSettlementService = financialSettlementService;
        _riskProtectionPolicyProvider = riskProtectionPolicyProvider;
        _commissionCalculator = commissionCalculator;
        _safetyPaymentReconciliationService = safetyPaymentReconciliationService;
        _options = options.Value;
    }

    public async Task<QrPaymentResult> CreateQrPaymentAsync(
        Guid customerId,
        long tripId,
        string? returnUrl,
        string? cancelUrl,
        CancellationToken cancellationToken)
    {
        var trip = await GetCustomerPayableTripAsync(customerId, tripId, cancellationToken);
        EnsureCustomerCanCreateQr(trip);
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
        var trip = await GetDriverPayableTripAsync(driverId, tripId, cancellationToken);
        EnsurePostTripPaymentStatus(trip);
        await FinalizeSuccessfulPaymentIfTripEndedAsync(trip, cancellationToken);
        return await CreateQrPaymentForTripAsync(
            trip,
            returnUrl,
            cancelUrl,
            cancellationToken);
    }

    public async Task<PaymentStatusResult> StartDriverPaymentAsync(
        Guid driverId,
        long tripId,
        CancellationToken cancellationToken)
    {
        var trip = await GetDriverPayableTripAsync(driverId, tripId, cancellationToken);
        EnsurePostTripPaymentStatus(trip);
        var result = await BuildStatusResultAsync(trip, cancellationToken, includeDriverFinancials: true);
        if (result.PaymentStatus == PaymentStatus.Success)
        {
            return result;
        }

        await _realtimeNotificationService.PublishTripPaymentPendingAsync(
            new TripPaymentPendingEvent(
                trip.Id,
                trip.BookingId,
                trip.Booking.CustomerId,
                trip.DriverId,
                PaymentId: null,
                PaymentMethod: null,
                PaymentStatus.Pending,
                result.Amount,
                result.Currency,
                trip.TripStatus,
                DateTime.UtcNow,
                "Tài xế đang chuẩn bị phương thức thanh toán. Vui lòng chờ để thanh toán.",
                trip.Booking.BookingStatus),
            cancellationToken);

        return result;
    }

    private async Task<QrPaymentResult> CreateQrPaymentForTripAsync(
        Trip trip,
        string? returnUrl,
        string? cancelUrl,
        CancellationToken cancellationToken)
    {
        var amounts = await GetPaymentAmountsAsync(trip, cancellationToken);
        var amount = amounts.FinalFare;
        if (amount <= 0)
        {
            if (!IsPostTripPaymentStatus(trip))
            {
                return new QrPaymentResult(
                    trip.Id, 0, "PAYMENT_AFTER_TRIP", 0m, Currency,
                    PaymentStatus.Pending, trip.TripStatus, null, null,
                    DateTime.UtcNow,
                    "Chuyến đi hiện không cần thanh toán trước. Số tiền cuối cùng sẽ được quyết toán sau khi chuyến đi kết thúc.");
            }

            trip = await SettleZeroPayAsync(trip.Id, cancellationToken);
            return new QrPaymentResult(
                trip.Id, 0, "NO_PAYMENT_REQUIRED", 0m, Currency,
                PaymentStatus.Success, trip.TripStatus, null, null,
                DateTime.UtcNow, "Khuyến mãi đã thanh toán toàn bộ chuyến đi.");
        }
        EnsurePayOsConfigured();

        var existingSuccess = IsSafetyTerminated(trip)
            ? null
            : trip.Payments.FirstOrDefault(x => x.PaymentStatus == PaymentStatus.Success);
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

        var payment = trip.Payments
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault(x => x.PaymentStatus == PaymentStatus.Pending);
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
        await FinalizeSuccessfulPaymentIfTripEndedAsync(trip, cancellationToken);
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
        return await BuildStatusResultAsync(trip, cancellationToken);
    }

    public async Task<PaymentStatusResult> GetDriverTripPaymentStatusAsync(
        Guid driverId,
        long tripId,
        CancellationToken cancellationToken)
    {
        var trip = await GetDriverPayableTripAsync(driverId, tripId, cancellationToken);
        await FinalizeSuccessfulPaymentIfTripEndedAsync(trip, cancellationToken);
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
        return await BuildStatusResultAsync(trip, cancellationToken, includeDriverFinancials: true);
    }

    public async Task<PaymentStatusResult> ConfirmCashPaymentAsync(
        Guid driverId,
        long tripId,
        CancellationToken cancellationToken)
    {
        var authorizedTrip = await GetDriverPayableTripAsync(driverId, tripId, cancellationToken);
        EnsurePostTripPaymentStatus(authorizedTrip);

        var result = await ExecuteSettlementTransactionAsync(
            async () =>
            {
                var trip = await GetDriverPayableTripAsync(driverId, tripId, cancellationToken);
                EnsurePostTripPaymentStatus(trip);
                return await ConfirmCashPaymentCoreAsync(trip, cancellationToken);
            },
            async () =>
            {
                var trip = await GetDriverPayableTripAsync(driverId, tripId, cancellationToken);
                var valid = await IsCommittedSettlementResultAsync(trip, requireSuccessfulPayment: false, cancellationToken);
                return (valid, new PaymentMutationResult(
                    trip,
                    trip.Payments.FirstOrDefault(x => x.PaymentStatus == PaymentStatus.Success),
                    ShouldPublish: false));
            },
            cancellationToken);

        if (result.ShouldPublish && result.Payment is not null)
            await PublishTripPaymentSucceededAsync(result.Trip, result.Payment, cancellationToken);
        return await BuildStatusResultAsync(result.Trip, cancellationToken, includeDriverFinancials: true);
    }

    private async Task<PaymentMutationResult> ConfirmCashPaymentCoreAsync(
        Trip trip,
        CancellationToken cancellationToken)
    {
        var isSafetySettlement = IsSafetyTerminated(trip);
        var existingSuccess = isSafetySettlement
            ? null
            : trip.Payments.FirstOrDefault(x => x.PaymentStatus == PaymentStatus.Success);
        if (existingSuccess is not null)
        {
            await FinalizeSuccessfulPaymentIfTripEndedAsync(trip, cancellationToken);
            return new PaymentMutationResult(trip, existingSuccess, ShouldPublish: false);
        }

        var settlement = await _financialSettlementService.GetOrCreateAsync(
            trip,
            isSafetySettlement,
            cancellationToken);
        var reconciliation = isSafetySettlement
            ? await _safetyPaymentReconciliationService.ReconcileAsync(trip, cancellationToken)
            : null;
        var amountToCollect = reconciliation?.RemainingPayableAmount
            ?? settlement.CustomerPayableAmount;
        if (amountToCollect <= 0m)
        {
            await _financialSettlementService.ApplyCashWalletAdjustmentAsync(trip, cancellationToken);
            if (trip.TripStatus == TripStatus.WAITING_PAYMENT)
                await AdvanceTripAfterPaymentAsync(trip, cancellationToken);
            return new PaymentMutationResult(
                trip,
                trip.Payments.OrderByDescending(x => x.PaidAt).FirstOrDefault(x => x.PaymentStatus == PaymentStatus.Success),
                ShouldPublish: false);
        }

        await _financialSettlementService.ApplyCashWalletAdjustmentAsync(trip, cancellationToken);

        Payment? payment = null;
        if (amountToCollect > 0)
        {
            payment = trip.Payments
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault(x => x.PaymentStatus == PaymentStatus.Pending) ?? new Payment
            {
                TripId = trip.Id,
                CreatedAt = DateTime.UtcNow
            };
            if (payment.Id == 0 && _dbContext.Entry(payment).State == EntityState.Detached)
                trip.Payments.Add(payment);
            payment.PaymentMethod = PaymentMethod.CASH;
            payment.TransactionReference = null;
            payment.Amount = amountToCollect;
            payment.Currency = Currency;
            payment.PaymentStatus = PaymentStatus.Success;
            payment.PaidAt = DateTime.UtcNow;
            payment.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (isSafetySettlement)
            await _safetyPaymentReconciliationService.ReconcileAsync(trip, cancellationToken);
        if (settlement.CustomerPayableAmount == 0 && trip.TripStatus == TripStatus.WAITING_PAYMENT)
            await AdvanceTripAfterPaymentAsync(trip, cancellationToken);
        else
            await FinalizeSuccessfulPaymentIfTripEndedAsync(trip, cancellationToken);

        return new PaymentMutationResult(trip, payment, ShouldPublish: payment is not null);
    }

    public async Task<PaymentStatusResult> ConfirmDemoQrPaymentAsync(
        DemoQrPaymentWebhookRequest request,
        CancellationToken cancellationToken)
    {
        var trip = await GetDemoQrPayableTripAsync(request.TripId, cancellationToken);
        if (trip.Payments.Any(x => x.PaymentStatus == PaymentStatus.Success))
        {
            await FinalizeSuccessfulPaymentIfTripEndedAsync(trip, cancellationToken);
            return await BuildStatusResultAsync(trip, cancellationToken);
        }

        var amount = (await GetPaymentAmountsAsync(trip, cancellationToken)).FinalFare;
        if (amount <= 0)
        {
            trip = await SettleZeroPayAsync(trip.Id, cancellationToken);
            return await BuildStatusResultAsync(trip, cancellationToken);
        }

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

        if (payment.Id == 0)
            await _dbContext.SaveChangesAsync(cancellationToken);

        await MarkQrPaymentSuccessAsync(
            trip,
            payment,
            amount,
            "mock-demo",
            cancellationToken);

        return await BuildStatusResultAsync(trip, cancellationToken);
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
        var paymentId = payment.Id;
        var mutation = await ExecuteSettlementTransactionAsync(
            async () =>
            {
                var currentPayment = !_dbContext.Database.IsRelational()
                    ? payment
                    : await GetPaymentForSettlementAsync(paymentId, cancellationToken);
                var currentTrip = currentPayment.Trip;
                if (currentPayment.PaymentStatus == PaymentStatus.Success)
                {
                    await FinalizeSuccessfulPaymentIfTripEndedAsync(currentTrip, cancellationToken);
                    return new PaymentMutationResult(currentTrip, currentPayment, ShouldPublish: false);
                }

                var expectedAmount = (await GetPaymentAmountsAsync(currentTrip, cancellationToken)).FinalFare;
                var normalizedPaidAmount = paidAmount > 0 ? ToVnd(paidAmount) : currentPayment.Amount;
                if (normalizedPaidAmount != expectedAmount)
                {
                    throw new BookingException(
                        "payment.amount_mismatch",
                        "Số tiền thanh toán không khớp với settlement của chuyến đi.",
                        StatusCodes.Status409Conflict);
                }

                currentPayment.PaymentStatus = PaymentStatus.Success;
                currentPayment.Amount = normalizedPaidAmount;
                currentPayment.PaidAt = DateTime.UtcNow;
                currentPayment.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                await FinalizeSuccessfulPaymentIfTripEndedAsync(
                    currentTrip,
                    cancellationToken,
                    providerReference);
                return new PaymentMutationResult(currentTrip, currentPayment, ShouldPublish: true);
            },
            async () =>
            {
                var currentPayment = !_dbContext.Database.IsRelational()
                    ? payment
                    : await GetPaymentForSettlementAsync(paymentId, cancellationToken);
                var valid = currentPayment.PaymentStatus == PaymentStatus.Success
                    && (!IsPostTripPaymentStatus(currentPayment.Trip)
                        || await IsCommittedSettlementResultAsync(
                            currentPayment.Trip,
                            requireSuccessfulPayment: true,
                            cancellationToken));
                return (valid, new PaymentMutationResult(
                    currentPayment.Trip,
                    currentPayment,
                    ShouldPublish: false));
            },
            cancellationToken);

        if (mutation.ShouldPublish && mutation.Payment is not null)
            await PublishTripPaymentSucceededAsync(mutation.Trip, mutation.Payment, cancellationToken);
        _dbContext.ChangeTracker.Clear();
        trip.TripStatus = mutation.Trip.TripStatus;
        trip.CompletedAt = mutation.Trip.CompletedAt;
        trip.Booking.BookingStatus = mutation.Trip.Booking.BookingStatus;
        payment.PaymentStatus = mutation.Payment?.PaymentStatus ?? payment.PaymentStatus;
        payment.PaidAt = mutation.Payment?.PaidAt ?? payment.PaidAt;
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

        if (!IsCustomerPaymentVisibleStatus(trip))
        {
            throw new BookingException(
                "payment.trip_not_waiting_payment",
                "Chuyến đi chưa sẵn sàng để thanh toán.",
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

        if (!IsCustomerPaymentVisibleStatus(trip))
        {
            throw new BookingException(
                "payment.trip_not_waiting_payment",
                "Chuyến đi chưa sẵn sàng để kiểm tra thanh toán.",
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

        if (!IsPostTripPaymentStatus(trip))
        {
            throw new BookingException(
                "payment.trip_not_waiting_payment",
                "Chuyến đi chưa sẵn sàng để xác nhận thanh toán.",
                StatusCodes.Status409Conflict);
        }

        return trip;
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

    private async Task AdvanceTripAfterPaymentAsync(
        Trip trip,
        CancellationToken cancellationToken)
    {
        if (trip.TripStatus is TripStatus.WAITING_RETURN_CONFIRM
            or TripStatus.RETURN_CONFIRMED
            or TripStatus.COMPLETED)
        {
            return;
        }

        await _tripStatusService.AdvanceAfterSuccessfulPaymentAsync(
            trip.DriverId,
            trip.Id,
            cancellationToken);
    }

    private async Task FinalizeSuccessfulPaymentIfTripEndedAsync(
        Trip trip,
        CancellationToken cancellationToken,
        string? providerReference = null)
    {
        if (_dbContext.Database.IsRelational()
            && _dbContext.Database.CurrentTransaction is null)
        {
            var settledTrip = await ExecuteSettlementTransactionAsync(
                async () =>
                {
                    var currentTrip = await GetTripForSettlementAsync(trip.Id, cancellationToken);
                    await FinalizeSuccessfulPaymentIfTripEndedAsync(
                        currentTrip,
                        cancellationToken,
                        providerReference);
                    return currentTrip;
                },
                async () =>
                {
                    var currentTrip = await GetTripForSettlementAsync(trip.Id, cancellationToken);
                    var valid = await IsCommittedSettlementResultAsync(
                        currentTrip,
                        requireSuccessfulPayment: true,
                        cancellationToken);
                    return (valid, currentTrip);
                },
                cancellationToken);
            _dbContext.ChangeTracker.Clear();
            trip.TripStatus = settledTrip.TripStatus;
            trip.CompletedAt = settledTrip.CompletedAt;
            trip.Booking.BookingStatus = settledTrip.Booking.BookingStatus;
            return;
        }

        var isSafetySettlement = trip.TripStatus == TripStatus.CANCELLED
            && trip.TerminationCategory == TripTerminationCategory.SAFETY
            && trip.ActualFare.HasValue;
        if (trip.TripStatus != TripStatus.WAITING_PAYMENT && !isSafetySettlement)
        {
            return;
        }

        var payment = trip.Payments
            .OrderByDescending(x => x.PaymentStatus == PaymentStatus.Success)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefault(x => x.PaymentStatus == PaymentStatus.Success);
        if (payment is null)
        {
            return;
        }

        if (isSafetySettlement)
        {
            await _safetyPaymentReconciliationService.ReconcileAsync(trip, cancellationToken);
            return;
        }

        if (payment.PaymentMethod == PaymentMethod.QR)
        {
            await _tripPaymentSettlementService.SettleSuccessfulQrPaymentAsync(
                trip,
                providerReference,
                cancellationToken);
        }

        if (!isSafetySettlement)
            await AdvanceTripAfterPaymentAsync(trip, cancellationToken);
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

    private async Task<TripPaymentAmounts> GetPaymentAmountsAsync(
        Trip trip,
        CancellationToken cancellationToken)
    {
        var snapshot = await _dbContext.TripFinancialSettlements
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TripId == trip.Id, cancellationToken);
        if (snapshot is not null)
        {
            if (IsSafetyTerminated(trip))
            {
                var reconciliation = await _dbContext.SafetyPaymentReconciliations
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.TripId == trip.Id, cancellationToken);
                if (reconciliation is not null)
                {
                    var remainingDriverShare = Math.Max(
                        0m, snapshot.DriverEarning - reconciliation.DriverCreditedAmount);
                    return new TripPaymentAmounts(
                        snapshot.CommissionBase,
                        reconciliation.RemainingPayableAmount,
                        remainingDriverShare,
                        Math.Max(0m, reconciliation.RemainingPayableAmount - remainingDriverShare));
                }
            }
            return new TripPaymentAmounts(
                snapshot.CommissionBase,
                snapshot.CustomerPayableAmount,
                snapshot.DriverEarning,
                snapshot.NetPlatformCommission);
        }

        if (IsPostTripPaymentStatus(trip) && trip.ActualFare.HasValue)
        {
            snapshot = await _financialSettlementService.GetOrCreateAsync(
                trip,
                IsSafetyTerminated(trip),
                cancellationToken);
            return new TripPaymentAmounts(
                snapshot.CommissionBase,
                snapshot.CustomerPayableAmount,
                snapshot.DriverEarning,
                snapshot.NetPlatformCommission);
        }

        var bookingPrice = BookingPriceMapper.FromBooking(trip.Booking);
        var originalFare = ToVnd(trip.ActualFare ?? bookingPrice.OriginalFare);
        var promotionExpense = trip.TripStatus == TripStatus.CANCELLED
                && trip.TerminationCategory == TripTerminationCategory.SAFETY
            ? 0m
            : trip.Booking.BookingPromotions.Sum(x => x.DiscountAmount);
        var policy = await _riskProtectionPolicyProvider.GetEffectivePolicyAsync(
            trip.StartedAt ?? DateTime.UtcNow,
            cancellationToken);
        var calculated = _commissionCalculator.Calculate(new CommissionCalculationInput(
            originalFare,
            promotionExpense,
            policy.BasePlatformCommissionRate,
            policy.RiskReserveRate,
            false));
        return new TripPaymentAmounts(
            calculated.CommissionBase,
            calculated.CustomerPayableAmount,
            calculated.DriverEarning,
            calculated.NetPlatformCommission);
    }

    private async Task<PaymentStatusResult> BuildStatusResultAsync(
        Trip trip,
        CancellationToken cancellationToken,
        bool includeDriverFinancials = false)
    {
        var amounts = await GetPaymentAmountsAsync(trip, cancellationToken);
        var settlement = includeDriverFinancials
            ? await _dbContext.TripFinancialSettlements.AsNoTracking()
                .SingleOrDefaultAsync(x => x.TripId == trip.Id, cancellationToken)
            : null;
        var payment = trip.Payments
            .OrderByDescending(x => x.PaymentStatus == PaymentStatus.Success)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefault();
        var reconciliation = IsSafetyTerminated(trip)
            ? await _dbContext.SafetyPaymentReconciliations.AsNoTracking()
                .Include(x => x.Refund)
                .SingleOrDefaultAsync(x => x.TripId == trip.Id, cancellationToken)
            : null;
        var zeroSettlementSucceeded = payment is null
            && amounts.FinalFare == 0
            && await _dbContext.TripFinancialSettlements.AsNoTracking()
                .AnyAsync(x => x.TripId == trip.Id && x.SettledAtUtc != null, cancellationToken);
        var displayStatus = reconciliation?.RemainingPayableAmount > 0m
            ? PaymentStatus.Pending
            : payment?.PaymentStatus ?? (zeroSettlementSucceeded ? PaymentStatus.Success : PaymentStatus.Pending);
        var displayAmount = reconciliation?.RemainingPayableAmount > 0m
            ? reconciliation.RemainingPayableAmount
            : payment?.Amount ?? amounts.FinalFare;
        return new PaymentStatusResult(
            trip.Id,
            payment?.Id,
            payment?.PaymentMethod,
            displayStatus,
            displayAmount,
            amounts.OriginalFare,
            amounts.FinalFare,
            includeDriverFinancials ? amounts.DriverShare : 0m,
            includeDriverFinancials ? amounts.PlatformShare : 0m,
            Currency,
            payment?.PaidAt,
            trip.TripStatus,
            BuildPaymentMessage(
                trip.TripStatus,
                displayStatus,
                reconciliation?.Status),
            reconciliation?.SuccessfulPaymentAmount ?? 0m,
            reconciliation?.RemainingPayableAmount ?? 0m,
            reconciliation?.RefundObligationAmount ?? 0m,
            reconciliation?.Status,
            reconciliation?.Refund?.Status,
            settlement?.DriverFareEarning,
            settlement?.LongDistanceEarning,
            settlement?.LongPickupCompensation,
            settlement?.DriverPayout ?? (includeDriverFinancials ? amounts.DriverShare : null));
    }

    private static bool IsCustomerPaymentVisibleStatus(Trip trip)
        => trip.TripStatus is not TripStatus.CANCELLED
            || trip.TerminationCategory == TripTerminationCategory.SAFETY;

    private static void EnsureCustomerCanCreateQr(Trip trip)
    {
        if (trip.TripStatus == TripStatus.CANCELLED
            && trip.TerminationCategory == TripTerminationCategory.SAFETY
            && trip.ActualFare.HasValue)
        {
            return;
        }
        if (trip.TripStatus is TripStatus.ACCEPTED
            or TripStatus.DRIVER_ARRIVING
            or TripStatus.ARRIVED)
        {
            return;
        }

        throw new BookingException(
            "payment.prepayment_window_closed",
            "Khách hàng chỉ có thể tạo mã QR để thanh toán trước khi chuyến đi bắt đầu.",
            StatusCodes.Status409Conflict);
    }

    private static bool IsPostTripPaymentStatus(Trip trip)
        => trip.TripStatus is TripStatus.WAITING_RETURN_CONFIRM
            or TripStatus.RETURN_CONFIRMED
            or TripStatus.WAITING_PAYMENT
            or TripStatus.COMPLETED
            || trip.TripStatus == TripStatus.CANCELLED
                && trip.TerminationCategory == TripTerminationCategory.SAFETY
                && trip.ActualFare.HasValue;

    private static void EnsurePostTripPaymentStatus(Trip trip)
    {
        if (!IsPostTripPaymentStatus(trip))
        {
            throw new BookingException(
                "payment.trip_not_waiting_payment",
                "Chỉ có thể thu tiền sau khi tài xế kết thúc chuyến đi.",
                StatusCodes.Status409Conflict);
        }
    }

    private static string BuildPaymentMessage(
        TripStatus tripStatus,
        PaymentStatus paymentStatus,
        SafetyPaymentReconciliationStatus? reconciliationStatus = null)
    {
        if (reconciliationStatus == SafetyPaymentReconciliationStatus.REFUND_PENDING)
            return "Khoản hoàn tiền đang chờ Nhân viên xử lý và cung cấp bằng chứng.";
        if (reconciliationStatus == SafetyPaymentReconciliationStatus.REFUNDED)
            return "Khoản hoàn tiền đã được xác nhận bằng chứng.";
        if (paymentStatus == PaymentStatus.Success || tripStatus == TripStatus.COMPLETED)
        {
            return "Thanh toán đã hoàn tất.";
        }

        if (tripStatus is TripStatus.ACCEPTED
            or TripStatus.DRIVER_ARRIVING
            or TripStatus.ARRIVED)
        {
            return "Bạn có thể thanh toán trước bằng PayOS hoặc thanh toán sau chuyến đi.";
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

    private async Task<Trip> SettleZeroPayAsync(long tripId, CancellationToken cancellationToken)
    {
        return await ExecuteSettlementTransactionAsync(
            async () =>
            {
                var trip = await GetTripForSettlementAsync(tripId, cancellationToken);
                var settlement = await _financialSettlementService.GetOrCreateAsync(
                    trip,
                    IsSafetyTerminated(trip),
                    cancellationToken);
                if (settlement.CustomerPayableAmount != 0)
                {
                    throw new BookingException(
                        "payment.amount_changed",
                        "Số tiền cần thanh toán đã thay đổi. Vui lòng tải lại trạng thái thanh toán.",
                        StatusCodes.Status409Conflict);
                }

                await _financialSettlementService.SettleQrDriverEarningAsync(
                    trip,
                    providerReference: "PLATFORM_PROMOTION",
                    cancellationToken);
                if (trip.TripStatus == TripStatus.WAITING_PAYMENT)
                    await AdvanceTripAfterPaymentAsync(trip, cancellationToken);
                return trip;
            },
            async () =>
            {
                var trip = await GetTripForSettlementAsync(tripId, cancellationToken);
                var valid = await IsCommittedSettlementResultAsync(
                    trip,
                    requireSuccessfulPayment: false,
                    cancellationToken);
                return (valid, trip);
            },
            cancellationToken);
    }

    private async Task<T> ExecuteSettlementTransactionAsync<T>(
        Func<Task<T>> operation,
        Func<Task<(bool IsValid, T Result)>> replay,
        CancellationToken cancellationToken)
    {
        var ownsTransaction = _dbContext.Database.IsRelational()
            && _dbContext.Database.CurrentTransaction is null;
        if (!ownsTransaction)
            return await operation();

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                _dbContext.ChangeTracker.Clear();
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                var result = await operation();
                await transaction.CommitAsync(cancellationToken);
                return result;
            });
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();
            var replayResult = await replay();
            if (replayResult.IsValid)
                return replayResult.Result;
            throw;
        }
    }

    private async Task<bool> IsCommittedSettlementResultAsync(
        Trip trip,
        bool requireSuccessfulPayment,
        CancellationToken cancellationToken)
    {
        var settlement = await _dbContext.TripFinancialSettlements.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TripId == trip.Id, cancellationToken);
        if (settlement?.SettledAtUtc is null)
            return false;

        var paymentStageCompleted = trip.TripStatus is TripStatus.WAITING_RETURN_CONFIRM
                or TripStatus.RETURN_CONFIRMED
                or TripStatus.COMPLETED
            || IsSafetyTerminated(trip);
        if (!paymentStageCompleted)
            return false;

        var successfulPayment = trip.Payments.FirstOrDefault(
            x => x.PaymentStatus == PaymentStatus.Success);
        if (requireSuccessfulPayment || settlement.CustomerPayableAmount > 0)
        {
            if (successfulPayment is null)
                return false;
        }

        var requiresWalletEffect = successfulPayment?.PaymentMethod switch
        {
            PaymentMethod.QR => settlement.DriverEarning > 0,
            PaymentMethod.CASH => settlement.CustomerPayableAmount != settlement.DriverEarning,
            _ => settlement.CustomerPayableAmount == 0 && settlement.DriverEarning > 0
        };
        if (requiresWalletEffect
            && !await _dbContext.WalletTransactions.AsNoTracking()
                .AnyAsync(
                    x => x.TripId == trip.Id && x.SettlementEffect != null,
                    cancellationToken))
        {
            return false;
        }

        if (trip.TripStatus == TripStatus.COMPLETED
            && settlement.IsRiskContributionEligible
            && settlement.RiskContribution > 0
            && !await _dbContext.RiskFundTransactions.AsNoTracking()
                .AnyAsync(
                    x => x.TripId == trip.Id
                        && x.TransactionType == RiskFundTransactionType.CONTRIBUTION,
                    cancellationToken))
        {
            return false;
        }

        return true;
    }

    private async Task<Trip> GetTripForSettlementAsync(
        long tripId,
        CancellationToken cancellationToken)
    {
        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .Include(x => x.Payments)
            .Include(x => x.WalletTransactions)
            .SingleOrDefaultAsync(x => x.Id == tripId, cancellationToken);
        return trip ?? throw new BookingException(
            "trip.not_found",
            "Không tìm thấy chuyến đi.",
            StatusCodes.Status404NotFound);
    }

    private async Task<Payment> GetPaymentForSettlementAsync(
        long paymentId,
        CancellationToken cancellationToken)
    {
        var payment = await _dbContext.Payments
            .Include(x => x.Trip)
                .ThenInclude(x => x.Booking)
                    .ThenInclude(x => x.BookingPromotions)
                        .ThenInclude(x => x.Promotion)
            .Include(x => x.Trip)
                .ThenInclude(x => x.Payments)
            .Include(x => x.Trip)
                .ThenInclude(x => x.WalletTransactions)
            .SingleOrDefaultAsync(x => x.Id == paymentId, cancellationToken);
        return payment ?? throw new BookingException(
            "payment.not_found",
            "Không tìm thấy giao dịch thanh toán.",
            StatusCodes.Status404NotFound);
    }

    private static bool IsSafetyTerminated(Trip trip) =>
        trip.TripStatus == TripStatus.CANCELLED
        && trip.TerminationCategory == TripTerminationCategory.SAFETY;

    private readonly record struct PaymentMutationResult(
        Trip Trip,
        Payment? Payment,
        bool ShouldPublish);

    private readonly record struct TripPaymentAmounts(
        decimal OriginalFare,
        decimal FinalFare,
        decimal DriverShare,
        decimal PlatformShare);

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
