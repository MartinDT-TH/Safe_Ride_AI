using System.Data;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class SafetyPaymentReconciliationService : ISafetyPaymentReconciliationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITripFinancialSettlementService _settlementService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public SafetyPaymentReconciliationService(
        ApplicationDbContext dbContext,
        ITripFinancialSettlementService settlementService,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _settlementService = settlementService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<SafetyPaymentReconciliation> ReconcileAsync(
        Trip trip,
        CancellationToken cancellationToken)
    {
        if (_dbContext.Database.IsRelational()
            && _dbContext.Database.CurrentTransaction is null)
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                var result = await ReconcileCoreAsync(trip, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            });
        }

        return await ReconcileCoreAsync(trip, cancellationToken);
    }

    private async Task<SafetyPaymentReconciliation> ReconcileCoreAsync(
        Trip trip,
        CancellationToken cancellationToken)
    {
        var isSafetyTermination = trip.TerminationCategory == TripTerminationCategory.SAFETY
            && trip.TripStatus == TripStatus.CANCELLED;
        var isFinalizedStandardTrip = trip.TerminationCategory != TripTerminationCategory.SAFETY
            && trip.TripStatus is TripStatus.WAITING_PAYMENT
                or TripStatus.WAITING_RETURN_CONFIRM
                or TripStatus.RETURN_CONFIRMED
                or TripStatus.COMPLETED;
        if (!isSafetyTermination && !isFinalizedStandardTrip)
            throw new BookingException(
                "payment.reconciliation_invalid_trip",
                "Chỉ có thể đối soát thanh toán cho chuyến đã hủy vì an toàn.", 409);

        var now = _dateTimeProvider.UtcNow;
        var successfulPayments = trip.Payments
            .Where(x => x.PaymentStatus == PaymentStatus.Success)
            .OrderBy(x => x.PaidAt ?? x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToList();
        var successfulAmount = successfulPayments.Sum(x => x.Amount);
        TripFinancialSettlement? settlement = null;
        var payable = 0m;
        if (trip.ActualFare.HasValue || isFinalizedStandardTrip)
        {
            settlement = await _settlementService.GetOrCreateAsync(
                trip, safetyTerminated: isSafetyTermination, cancellationToken);
            payable = settlement.CustomerPayableAmount;
        }

        var remaining = Math.Max(0m, payable - successfulAmount);
        var refundAmount = Math.Max(0m, successfulAmount - payable);
        var reconciliation = await _dbContext.SafetyPaymentReconciliations
            .Include(x => x.Refund)
            .SingleOrDefaultAsync(x => x.TripId == trip.Id, cancellationToken);
        if (reconciliation is null)
        {
            reconciliation = new SafetyPaymentReconciliation
            {
                TripId = trip.Id,
                CreatedAtUtc = now
            };
            _dbContext.SafetyPaymentReconciliations.Add(reconciliation);
        }

        reconciliation.CustomerPayableAmount = payable;
        reconciliation.SuccessfulPaymentAmount = successfulAmount;
        reconciliation.RemainingPayableAmount = remaining;
        reconciliation.RefundObligationAmount = refundAmount;
        reconciliation.UpdatedAtUtc = now;

        if (refundAmount > 0m)
        {
            var refundablePayment = successfulPayments.Last();
            if (reconciliation.Refund is null)
            {
                reconciliation.Refund = new ManualPaymentRefund
                {
                    PaymentId = refundablePayment.Id,
                    Amount = refundAmount,
                    Status = ManualRefundStatus.REFUND_PENDING,
                    CreatedAtUtc = now
                };
                _dbContext.Notifications.Add(new Notification
                {
                    UserId = trip.Booking.CustomerId,
                    Title = "Hoàn tiền chuyến đi đang chờ xử lý",
                    Content = $"SafeRide đang xử lý khoản hoàn {refundAmount:0}đ cho chuyến #{trip.Id}.",
                    NotificationType = "TripRefundPending",
                    ReferenceId = trip.Id,
                    SentAt = now
                });
            }
            else if (reconciliation.Refund.Status == ManualRefundStatus.REFUND_PENDING)
            {
                reconciliation.Refund.PaymentId = refundablePayment.Id;
                reconciliation.Refund.Amount = refundAmount;
            }
            else if (reconciliation.Refund.Amount != refundAmount)
            {
                throw new BookingException(
                    "payment.refund_obligation_changed_after_refund",
                    "The calculated refund obligation changed after the refund was completed.",
                    409);
            }
        }

        reconciliation.Status = refundAmount > 0m
            ? reconciliation.Refund?.Status == ManualRefundStatus.REFUNDED
                ? SafetyPaymentReconciliationStatus.REFUNDED
                : SafetyPaymentReconciliationStatus.REFUND_PENDING
            : remaining > 0m
                ? SafetyPaymentReconciliationStatus.PAYMENT_PENDING
                : payable == 0m && successfulAmount == 0m
                    ? SafetyPaymentReconciliationStatus.NOT_REQUIRED
                    : SafetyPaymentReconciliationStatus.PAID;

        if (settlement is not null && isSafetyTermination)
        {
            var successfulQrAmount = successfulPayments
                .Where(x => x.PaymentMethod == PaymentMethod.QR)
                .Sum(x => x.Amount);
            var targetDriverCredit = Math.Min(
                settlement.DriverEarning,
                Math.Min(successfulQrAmount, payable));
            await ApplyDriverCreditAsync(trip, reconciliation, targetDriverCredit, cancellationToken);
            if (remaining == 0m)
                settlement.SettledAtUtc ??= now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return reconciliation;
    }

    public async Task<IReadOnlyList<ManualRefundQueueItemResponse>> ListRefundsAsync(
        ManualRefundStatus? status,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.ManualPaymentRefunds.AsNoTracking()
            .Include(x => x.Reconciliation)
            .AsQueryable();
        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);
        var refunds = await query
            .OrderBy(x => x.Status == ManualRefundStatus.REFUND_PENDING ? 0 : 1)
            .ThenBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Take(200)
            .ToListAsync(cancellationToken);
        return refunds.Select(x => new ManualRefundQueueItemResponse(
                x.Id,
                x.Reconciliation.TripId,
                x.PaymentId,
                x.Amount,
                x.Status,
                x.PaymentReference,
                x.EvidenceUrl,
                x.RefundedByUserId,
                x.CreatedAtUtc,
                x.RefundedAtUtc,
                Convert.ToBase64String(x.RowVersion)))
            .ToList();
    }

    public async Task<SafetyPaymentReconciliationResponse> ConfirmManualRefundAsync(
        Guid staffUserId,
        long refundId,
        ManualRefundConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        var paymentReference = request.PaymentReference?.Trim();
        var evidenceUrl = request.EvidenceUrl?.Trim();
        var idempotencyKey = request.IdempotencyKey?.Trim();
        if (string.IsNullOrWhiteSpace(paymentReference)
            || string.IsNullOrWhiteSpace(evidenceUrl)
            || string.IsNullOrWhiteSpace(idempotencyKey))
            throw new BookingException(
                "payment.refund_evidence_required",
                "Cần mã giao dịch, bằng chứng hoàn tiền và khóa idempotency.", 400);
        if (paymentReference.Length > 200 || evidenceUrl.Length > 1000 || idempotencyKey.Length > 100)
            throw new BookingException(
                "payment.refund_metadata_too_long",
                "Thông tin đối soát hoàn tiền vượt quá độ dài cho phép.", 400);
        if (!Uri.TryCreate(evidenceUrl, UriKind.Absolute, out var evidenceUri)
            || evidenceUri.Scheme != Uri.UriSchemeHttps)
            throw new BookingException(
                "payment.refund_evidence_invalid",
                "Đường dẫn bằng chứng hoàn tiền phải dùng HTTPS.", 400);

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = _dbContext.Database.IsRelational()
                ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            var keyOwner = await _dbContext.ManualPaymentRefunds.AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.ConfirmationIdempotencyKey == idempotencyKey,
                    cancellationToken);
            if (keyOwner is not null && keyOwner.Id != refundId)
                throw new BookingException(
                    "payment.refund_idempotency_conflict",
                    "Khóa idempotency đã được dùng cho nghĩa vụ hoàn tiền khác.", 409);
            var refund = await _dbContext.ManualPaymentRefunds
                .Include(x => x.Reconciliation)
                .SingleOrDefaultAsync(x => x.Id == refundId, cancellationToken)
                ?? throw new BookingException(
                    "payment.refund_not_found", "Không tìm thấy nghĩa vụ hoàn tiền.", 404);

            if (refund.Status == ManualRefundStatus.REFUNDED)
            {
                if (!string.Equals(refund.ConfirmationIdempotencyKey, idempotencyKey, StringComparison.Ordinal)
                    || !string.Equals(refund.PaymentReference, paymentReference, StringComparison.Ordinal)
                    || !string.Equals(refund.EvidenceUrl, evidenceUrl, StringComparison.Ordinal))
                    throw new BookingException(
                        "payment.refund_idempotency_conflict",
                        "Khóa idempotency đã được dùng với nội dung hoàn tiền khác.", 409);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                return ToResponse(refund.Reconciliation);
            }

            var expectedRowVersion = DecodeRowVersion(request.RowVersion);
            _dbContext.Entry(refund).Property(x => x.RowVersion).OriginalValue = expectedRowVersion;
            refund.Status = ManualRefundStatus.REFUNDED;
            refund.PaymentReference = paymentReference;
            refund.EvidenceUrl = evidenceUrl;
            refund.RefundedByUserId = staffUserId;
            refund.ConfirmationIdempotencyKey = idempotencyKey;
            refund.RefundedAtUtc = _dateTimeProvider.UtcNow;
            refund.Reconciliation.Status = SafetyPaymentReconciliationStatus.REFUNDED;
            refund.Reconciliation.UpdatedAtUtc = _dateTimeProvider.UtcNow;
            var customerId = await _dbContext.Trips.AsNoTracking()
                .Where(x => x.Id == refund.Reconciliation.TripId)
                .Select(x => x.Booking.CustomerId)
                .SingleAsync(cancellationToken);
            _dbContext.Notifications.Add(new Notification
            {
                UserId = customerId,
                Title = "Đã hoàn tiền chuyến đi",
                Content = $"Khoản hoàn {refund.Amount:0}đ cho chuyến #{refund.Reconciliation.TripId} đã hoàn tất.",
                NotificationType = "TripRefundCompleted",
                ReferenceId = refund.Reconciliation.TripId,
                SentAt = _dateTimeProvider.UtcNow
            });
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new BookingException(
                    "payment.refund_concurrency_conflict",
                    "Nghĩa vụ hoàn tiền đã thay đổi. Vui lòng tải lại.", 409);
            }
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return ToResponse(refund.Reconciliation);
        });
    }

    private async Task ApplyDriverCreditAsync(
        Trip trip,
        SafetyPaymentReconciliation reconciliation,
        decimal targetCredit,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.WalletTransactions.SingleOrDefaultAsync(
            x => x.TripId == trip.Id && x.SettlementEffect == WalletSettlementEffect.QrDriverEarning,
            cancellationToken);
        var alreadyCredited = existing?.Amount ?? 0m;
        if (targetCredit < alreadyCredited)
            throw new BookingException(
                "payment.driver_credit_exceeds_snapshot",
                "Khoản đã ghi có cho tài xế vượt mức đối soát an toàn.", 409);
        var delta = targetCredit - alreadyCredited;
        if (delta > 0m)
        {
            var wallet = await _dbContext.DriverWallets.SingleOrDefaultAsync(
                x => x.DriverId == trip.DriverId, cancellationToken);
            if (wallet is null)
            {
                wallet = new DriverWallet { DriverId = trip.DriverId, CurrentBalance = 0m };
                _dbContext.DriverWallets.Add(wallet);
            }
            wallet.CurrentBalance += delta;
            if (existing is null)
            {
                _dbContext.WalletTransactions.Add(new WalletTransaction
                {
                    Wallet = wallet,
                    TripId = trip.Id,
                    TransactionType = WalletTransactionType.Income,
                    SettlementEffect = WalletSettlementEffect.QrDriverEarning,
                    Amount = targetCredit,
                    Description = "SafeRide safety-cancelled QR payout",
                    CreatedAt = _dateTimeProvider.UtcNow
                });
            }
            else
            {
                existing.Amount = targetCredit;
                existing.Description = "SafeRide safety-cancelled QR payout (reconciled)";
            }
        }
        reconciliation.DriverCreditedAmount = targetCredit;
    }

    private static byte[] DecodeRowVersion(string value)
    {
        try { return Convert.FromBase64String(value ?? string.Empty); }
        catch (FormatException)
        {
            throw new BookingException(
                "payment.refund_row_version_invalid", "RowVersion hoàn tiền không hợp lệ.", 400);
        }
    }

    private static SafetyPaymentReconciliationResponse ToResponse(SafetyPaymentReconciliation reconciliation) =>
        new(
            reconciliation.TripId,
            reconciliation.CustomerPayableAmount,
            reconciliation.SuccessfulPaymentAmount,
            reconciliation.RemainingPayableAmount,
            reconciliation.RefundObligationAmount,
            reconciliation.DriverCreditedAmount,
            reconciliation.Status,
            reconciliation.Refund?.Id,
            reconciliation.Refund?.Status,
            Convert.ToBase64String(reconciliation.Refund?.RowVersion ?? reconciliation.RowVersion));
}
