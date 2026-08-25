using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.Ratings;
using SafeRide.Application.Features.RiskProtection;
using SafeRide.Application.Features.Trips.DTOs;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using System.Data;
using System.Text.Json;

namespace SafeRide.Infrastructure.Services;

public sealed class TripStatusService : ITripStatusService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IRedisService _redisService;
    private readonly IRealtimeNotificationService _realtimeNotificationService;
    private readonly ITripReturnEvidenceStorage _tripReturnEvidenceStorage;
    private readonly IEvidenceFileValidator _evidenceFileValidator;
    private readonly ITripSharingService _tripSharingService;
    private readonly IOptionsMonitor<TripTrackingOptions> _options;
    private readonly IMapRoutingService _mapRoutingService;
    private readonly TripFareFinalizationService _tripFareFinalizationService;
    private readonly TripPaymentSettlementService _tripPaymentSettlementService;
    private readonly IPreTripVehicleCheckService _preTripVehicleCheckService;
    private readonly ITripFinancialSettlementService _financialSettlementService;
    private readonly ISafetyPaymentReconciliationService _safetyPaymentReconciliationService;
    private readonly IAccountBanEvaluationService _accountBanEvaluationService;
    private readonly ILogger<TripStatusService> _logger;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public TripStatusService(
        ApplicationDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IRedisService redisService,
        IRealtimeNotificationService realtimeNotificationService,
        ITripReturnEvidenceStorage tripReturnEvidenceStorage,
        IEvidenceFileValidator evidenceFileValidator,
        ITripSharingService tripSharingService,
        IOptionsMonitor<TripTrackingOptions> options,
        IMapRoutingService mapRoutingService,
        TripFareFinalizationService tripFareFinalizationService,
        TripPaymentSettlementService tripPaymentSettlementService,
        IPreTripVehicleCheckService preTripVehicleCheckService,
        ITripFinancialSettlementService financialSettlementService,
        IAccountBanEvaluationService accountBanEvaluationService,
        ILogger<TripStatusService> logger)
        : this(
            dbContext, dateTimeProvider, redisService, realtimeNotificationService,
            tripReturnEvidenceStorage, evidenceFileValidator, tripSharingService, options, mapRoutingService,
            tripFareFinalizationService, tripPaymentSettlementService,
            preTripVehicleCheckService, financialSettlementService,
            new SafetyPaymentReconciliationService(
                dbContext, financialSettlementService, dateTimeProvider),
            accountBanEvaluationService, logger)
    {
    }

    public TripStatusService(
        ApplicationDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IRedisService redisService,
        IRealtimeNotificationService realtimeNotificationService,
        ITripReturnEvidenceStorage tripReturnEvidenceStorage,
        IEvidenceFileValidator evidenceFileValidator,
        ITripSharingService tripSharingService,
        IOptionsMonitor<TripTrackingOptions> options,
        IMapRoutingService mapRoutingService,
        TripFareFinalizationService tripFareFinalizationService,
        TripPaymentSettlementService tripPaymentSettlementService,
        IPreTripVehicleCheckService preTripVehicleCheckService,
        ITripFinancialSettlementService financialSettlementService,
        ISafetyPaymentReconciliationService safetyPaymentReconciliationService,
        IAccountBanEvaluationService accountBanEvaluationService,
        ILogger<TripStatusService> logger)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _redisService = redisService;
        _realtimeNotificationService = realtimeNotificationService;
        _tripReturnEvidenceStorage = tripReturnEvidenceStorage;
        _evidenceFileValidator = evidenceFileValidator;
        _tripSharingService = tripSharingService;
        _options = options;
        _mapRoutingService = mapRoutingService;
        _tripFareFinalizationService = tripFareFinalizationService;
        _tripPaymentSettlementService = tripPaymentSettlementService;
        _preTripVehicleCheckService = preTripVehicleCheckService;
        _financialSettlementService = financialSettlementService;
        _safetyPaymentReconciliationService = safetyPaymentReconciliationService;
        _accountBanEvaluationService = accountBanEvaluationService;
        _logger = logger;
    }

    public async Task UpdateDriverTripStatusAsync(
        Guid driverId,
        long tripId,
        TripStatus tripStatus,
        CancellationToken cancellationToken)
    {
        if (tripStatus == TripStatus.WAITING_PAYMENT)
        {
            throw new BookingException(
                "trip.end_workflow_required",
                "Hãy dùng quy trình kết thúc chuyến để chốt cước trước khi thanh toán.",
                409);
        }

        if (tripStatus == TripStatus.WAITING_RETURN_CONFIRM)
        {
            await EndTripAsync(driverId, tripId, cancellationToken);
            return;
        }

        // Flow: load the driver's trip with promotion state so terminal transitions can settle usage.
        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .FirstOrDefaultAsync(
                x => x.Id == tripId && x.DriverId == driverId,
                cancellationToken);
        if (trip is null)
        {
            throw new BookingException(
                "trip.not_found",
                "Khong tim thay chuyen di cua tai xe.",
                404);
        }

        try
        {
            await ApplyTripStatusAsync(
                trip,
                tripStatus,
                driverId,
                cancellationToken);
        }
        catch (DbUpdateException exception)
            when (tripStatus == TripStatus.IN_PROGRESS
                && IsTripCoverageUniqueConstraintViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();
            var alreadyStartedWithCoverage = await _dbContext.Trips.AsNoTracking()
                .AnyAsync(
                    x => x.Id == tripId
                        && x.DriverId == driverId
                        && x.TripStatus == TripStatus.IN_PROGRESS,
                    cancellationToken)
                && await _dbContext.TripProtectionCoverages.AsNoTracking()
                    .AnyAsync(x => x.TripId == tripId, cancellationToken);
            if (!alreadyStartedWithCoverage) throw;

            _logger.LogInformation(
                "Concurrent start for trip {TripId} reused its existing protection coverage.",
                tripId);
        }
    }

    public async Task CompleteTripAsync(
        Guid userId,
        long tripId,
        CancellationToken cancellationToken)
    {
        var ownsTransaction = _dbContext.Database.IsRelational()
            && _dbContext.Database.CurrentTransaction is null;
        if (!ownsTransaction)
        {
            await CompleteTripCoreAsync(userId, tripId, cancellationToken);
            return;
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                _dbContext.ChangeTracker.Clear();
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
                await CompleteTripCoreAsync(userId, tripId, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            });
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();
            if (!await IsCompletedSettlementReplayAsync(userId, tripId, cancellationToken))
            {
                throw;
            }

            _logger.LogInformation(
                "Concurrent completion for trip {TripId} replayed the committed settlement.",
                tripId);
        }
    }

    private async Task CompleteTripCoreAsync(
        Guid userId,
        long tripId,
        CancellationToken cancellationToken)
    {
        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .Include(x => x.Booking)
                .ThenInclude(x => x.PricingRule)
            .Include(x => x.Booking)
                .ThenInclude(x => x.SurgePricingRule)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(
                x => x.Id == tripId
                    && (x.DriverId == userId || x.Booking.CustomerId == userId),
                cancellationToken);
        if (trip is null)
        {
            throw new BookingException(
                "trip.not_found",
                "Không tìm thấy chuyến đi.",
                404);
        }

        if (trip.TripStatus == TripStatus.COMPLETED)
        {
            return;
        }

        if (trip.TripStatus != TripStatus.RETURN_CONFIRMED)
        {
            throw new BookingException(
                "trip.invalid_status_transition",
                "Chi co the hoan tat chuyen sau khi da xac nhan tra xe.",
                409);
        }

        await EnsurePaymentSucceededAsync(trip, cancellationToken);

        await ApplyTripStatusAsync(
            trip,
            TripStatus.COMPLETED,
            userId,
            cancellationToken);
    }

    public async Task AdvanceAfterSuccessfulPaymentAsync(
        Guid userId,
        long tripId,
        CancellationToken cancellationToken)
    {
        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(
                x => x.Id == tripId
                    && (x.DriverId == userId || x.Booking.CustomerId == userId),
                cancellationToken);
        if (trip is null)
        {
            throw new BookingException(
                "trip.not_found",
                "Khong tim thay chuyen di.",
                404);
        }

        if (trip.TripStatus is TripStatus.WAITING_RETURN_CONFIRM
            or TripStatus.RETURN_CONFIRMED
            or TripStatus.COMPLETED)
        {
            return;
        }

        if (trip.TripStatus != TripStatus.WAITING_PAYMENT)
        {
            throw new BookingException(
                "trip.invalid_status_transition",
                "Chi co the xac nhan thanh toan khi chuyen dang cho thanh toan.",
                409);
        }

        await EnsurePaymentSucceededAsync(trip, cancellationToken);
        await ApplyTripStatusAsync(
            trip,
            TripStatus.WAITING_RETURN_CONFIRM,
            userId,
            cancellationToken);
    }

    private async Task<bool> IsCompletedSettlementReplayAsync(
        Guid userId,
        long tripId,
        CancellationToken cancellationToken)
    {
        var trip = await _dbContext.Trips.AsNoTracking()
            .Where(x => x.Id == tripId
                && (x.DriverId == userId || x.Booking.CustomerId == userId))
            .Select(x => new { x.TripStatus })
            .SingleOrDefaultAsync(cancellationToken);
        if (trip?.TripStatus != TripStatus.COMPLETED)
        {
            return false;
        }

        var settlement = await _dbContext.TripFinancialSettlements.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TripId == tripId, cancellationToken);
        if (settlement?.SettledAtUtc is null)
        {
            return false;
        }

        var paymentMethod = await _dbContext.Payments.AsNoTracking()
            .Where(x => x.TripId == tripId && x.PaymentStatus == PaymentStatus.Success)
            .Select(x => (PaymentMethod?)x.PaymentMethod)
            .FirstOrDefaultAsync(cancellationToken);
        var requiresWalletEffect = paymentMethod switch
        {
            PaymentMethod.QR => settlement.DriverEarning > 0,
            PaymentMethod.CASH => settlement.CustomerPayableAmount != settlement.DriverEarning,
            _ => settlement.CustomerPayableAmount == 0 && settlement.DriverEarning > 0
        };
        if (requiresWalletEffect
            && !await _dbContext.WalletTransactions.AsNoTracking()
                .AnyAsync(
                    x => x.TripId == tripId && x.SettlementEffect != null,
                    cancellationToken))
        {
            return false;
        }

        return !settlement.IsRiskContributionEligible
            || settlement.RiskContribution <= 0
            || await _dbContext.RiskFundTransactions.AsNoTracking()
                .AnyAsync(
                    x => x.TripId == tripId
                        && x.TransactionType == RiskFundTransactionType.CONTRIBUTION,
                    cancellationToken);
    }

    public async Task EndTripAsync(
        Guid driverId,
        long tripId,
        CancellationToken cancellationToken,
        TripEndReason reason = TripEndReason.NORMAL_COMPLETION)
    {
        ValidateDriverEndReason(reason);
        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
            .Include(x => x.Booking)
                .ThenInclude(x => x.PricingRule)
            .Include(x => x.Booking)
                .ThenInclude(x => x.SurgePricingRule)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(
                x => x.Id == tripId && x.DriverId == driverId,
                cancellationToken);
        if (trip is null)
        {
            throw new BookingException(
                "trip.not_found",
                "Khong tim thay chuyen di cua tai xe.",
                404);
        }

        if (trip.TripStatus != TripStatus.IN_PROGRESS)
        {
            throw new BookingException(
                "trip.invalid_status_transition",
                "Chi co the ket thuc chuyen khi chuyen dang di chuyen.",
                409);
        }

        if (await _dbContext.TripEndReconciliationRequests.AnyAsync(
                x => x.TripId == trip.Id
                    && x.Status == TripEndReconciliationStatus.PENDING,
                cancellationToken))
        {
            throw new BookingException(
                "trip.end_reconciliation_pending",
                "A staff reconciliation request is already pending for this trip.",
                409);
        }

        await FinalizeTripAsync(
            trip,
            driverId,
            cancellationToken,
            reason);

        await ApplyTripStatusAsync(
            trip,
            TripStatus.WAITING_PAYMENT,
            driverId,
            cancellationToken);

        if (await HasPaymentSucceededAsync(trip, cancellationToken))
        {
            await ApplyTripStatusAsync(
                trip,
                TripStatus.WAITING_RETURN_CONFIRM,
                driverId,
                cancellationToken);
        }

    }

    public async Task<TripEndReconciliationResult> RequestEndTripReconciliationAsync(
        Guid driverId,
        long tripId,
        TripEndReason reason,
        CancellationToken cancellationToken)
    {
        if (reason is not TripEndReason.DRIVER_UNABLE_TO_CONTINUE
            and not TripEndReason.STARTED_BY_MISTAKE)
            throw new BookingException(
                "trip.end_reconciliation_reason_not_allowed",
                "This end reason cannot be submitted for staff reconciliation.", 400);

        var trip = await _dbContext.Trips.AsNoTracking().FirstOrDefaultAsync(
            x => x.Id == tripId && x.DriverId == driverId, cancellationToken);
        if (trip is null)
            throw new BookingException("trip.not_found", "Trip not found.", 404);
        if (trip.TripStatus != TripStatus.IN_PROGRESS)
            throw new BookingException(
                "trip.invalid_status_transition",
                "Only an in-progress trip can request end reconciliation.", 409);

        var existing = await _dbContext.TripEndReconciliationRequests.AsNoTracking()
            .OrderByDescending(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync(
                x => x.TripId == tripId
                    && x.Status == TripEndReconciliationStatus.PENDING,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestedReason == reason)
                return ToEndReconciliationResult(existing);
            throw new BookingException(
                "trip.end_reconciliation_conflict",
                "A different end-reason reconciliation is already pending.", 409);
        }

        var request = new Domain.Entities.TripEndReconciliationRequest
        {
            TripId = tripId,
            RequestedReason = reason,
            RequestedByDriverId = driverId,
            RequestedAtUtc = _dateTimeProvider.UtcNow
        };
        _dbContext.TripEndReconciliationRequests.Add(request);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            IsEndReconciliationUniqueConstraintViolation(exception))
        {
            _dbContext.ChangeTracker.Clear();
            var winner = await _dbContext.TripEndReconciliationRequests.AsNoTracking()
                .SingleAsync(
                    x => x.TripId == tripId
                        && x.Status == TripEndReconciliationStatus.PENDING,
                    cancellationToken);
            if (winner.RequestedReason == reason)
                return ToEndReconciliationResult(winner);
            throw new BookingException(
                "trip.end_reconciliation_conflict",
                "A different end-reason reconciliation is already pending.", 409);
        }

        return ToEndReconciliationResult(request);
    }

    public async Task<TripEndReconciliationResult> ResolveEndTripReconciliationAsync(
        Guid staffUserId,
        long tripId,
        long requestId,
        bool approved,
        string? resolutionNote,
        CancellationToken cancellationToken)
    {
        var request = await _dbContext.TripEndReconciliationRequests
            .Include(x => x.Trip)
                .ThenInclude(x => x.Booking)
                    .ThenInclude(x => x.BookingPromotions)
                        .ThenInclude(x => x.Promotion)
            .Include(x => x.Trip.Booking.PricingRule)
            .Include(x => x.Trip.Booking.SurgePricingRule)
            .Include(x => x.Trip.Payments)
            .SingleOrDefaultAsync(
                x => x.Id == requestId && x.TripId == tripId,
                cancellationToken)
            ?? throw new BookingException(
                "trip.end_reconciliation_not_found",
                "End-reason reconciliation request not found.", 404);
        var targetStatus = approved
            ? TripEndReconciliationStatus.APPROVED
            : TripEndReconciliationStatus.REJECTED;
        if (request.Status != TripEndReconciliationStatus.PENDING)
        {
            if (request.Status == targetStatus)
                return ToEndReconciliationResult(request);
            throw new BookingException(
                "trip.end_reconciliation_already_resolved",
                "The request has already been resolved with a different decision.", 409);
        }

        request.Status = targetStatus;
        request.ResolvedByStaffId = staffUserId;
        request.ResolvedAtUtc = _dateTimeProvider.UtcNow;
        request.ResolutionNote = string.IsNullOrWhiteSpace(resolutionNote)
            ? null
            : resolutionNote.Trim();
        if (request.ResolutionNote?.Length > 1000)
            throw new BookingException(
                "trip.end_reconciliation_note_too_long",
                "Resolution note cannot exceed 1000 characters.", 400);

        if (!approved)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ToEndReconciliationResult(request);
        }
        if (request.Trip.TripStatus != TripStatus.IN_PROGRESS)
            throw new BookingException(
                "trip.end_reconciliation_invalid_status",
                "The trip is no longer in progress.", 409);

        await FinalizeTripAsync(
            request.Trip, staffUserId, cancellationToken, request.RequestedReason);
        await ApplyTripStatusAsync(
            request.Trip, TripStatus.WAITING_PAYMENT, staffUserId, cancellationToken);
        if (await HasPaymentSucceededAsync(request.Trip, cancellationToken))
        {
            await ApplyTripStatusAsync(
                request.Trip,
                TripStatus.WAITING_RETURN_CONFIRM,
                staffUserId,
                cancellationToken);
        }
        return ToEndReconciliationResult(request);
    }

    public async Task ConfirmReturnByCustomerAsync(
        Guid customerId,
        long tripId,
        bool vehicleReturnedConfirmed,
        CancellationToken cancellationToken,
        int? ratingScore = null,
        string? comment = null)
    {
        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .Include(x => x.Booking)
                .ThenInclude(x => x.PricingRule)
            .Include(x => x.Booking)
                .ThenInclude(x => x.SurgePricingRule)
            .Include(x => x.Payments)
            .Include(x => x.Rating)
            .Include(x => x.ReturnConfirmations)
            .FirstOrDefaultAsync(
                x => x.Id == tripId && x.Booking.CustomerId == customerId,
                cancellationToken);
        if (trip is null)
        {
            throw new BookingException(
                "trip.not_found",
                "Khong tim thay chuyen di cua khach hang.",
                404);
        }

        var existingCustomerConfirmation = trip.ReturnConfirmations
            .OrderByDescending(x => x.ConfirmedAt)
            .FirstOrDefault(x =>
                x.ConfirmedByUserId == customerId
                && x.HandoverStatus == HandoverStatus.CustomerConfirmed);
        if ((trip.TripStatus is TripStatus.RETURN_CONFIRMED or TripStatus.COMPLETED)
            && existingCustomerConfirmation is not null)
        {
            if (trip.TripStatus == TripStatus.RETURN_CONFIRMED)
            {
                await CompleteTripAsync(customerId, trip.Id, cancellationToken);
            }

            await EvaluateSubmittedRatingAsync(
                trip.Id,
                customerId,
                trip.DriverId,
                cancellationToken);
            await CleanupTripTrackingAsync(trip.Id, cancellationToken);
            return;
        }

        if (trip.TripStatus != TripStatus.WAITING_RETURN_CONFIRM)
        {
            throw new BookingException(
                "trip.invalid_status_transition",
                "Chi co the xac nhan tra xe khi chuyen dang cho xac nhan.",
                409);
        }

        if (!vehicleReturnedConfirmed)
        {
            throw new BookingException(
                "trip.return_confirmation_required",
                "Vui lòng xác nhận trả xe để hoàn tất chuyến đi.",
                400);
        }

        if (ratingScore is null)
        {
            throw new RatingException(
                "rating.required",
                "Vui lòng đánh giá tài xế khi xác nhận trả xe.",
                400);
        }

        if (ratingScore is not null)
        {
            if (ratingScore is < 1 or > 5)
            {
                throw new RatingException(
                    "rating.invalid_score",
                    "Điểm đánh giá phải từ 1 đến 5.",
                    400);
            }

            if (trip.Rating is not null)
            {
                throw new RatingException(
                    "rating.already_submitted",
                    "Chuyến đi này đã được đánh giá.",
                    409);
            }
        }

        await EnsurePaymentSucceededAsync(trip, cancellationToken);

        if (!trip.EndedAt.HasValue || !trip.FinalFare.HasValue)
        {
            await FinalizeTripAsync(
                trip,
                customerId,
                cancellationToken,
                trip.EndReason ?? TripEndReason.NORMAL_COMPLETION);
        }

        var utcNow = _dateTimeProvider.UtcNow;
        _dbContext.TripReturnConfirmations.Add(new Domain.Entities.TripReturnConfirmation
        {
            TripId = trip.Id,
            DriverId = trip.DriverId,
            ConfirmedByUserId = customerId,
            HandoverStatus = HandoverStatus.CustomerConfirmed,
            ConfirmedAt = utcNow,
            CreatedAt = utcNow
        });

        if (ratingScore is not null)
        {
            var normalizedComment = comment?.Trim();
            _dbContext.Ratings.Add(new Domain.Entities.Rating
            {
                TripId = trip.Id,
                CustomerId = customerId,
                DriverId = trip.DriverId,
                RatingScore = ratingScore.Value,
                Comment = string.IsNullOrWhiteSpace(normalizedComment)
                    ? null
                    : normalizedComment,
                CreatedAt = utcNow
            });
        }

        await ApplyTripStatusAsync(
            trip,
            TripStatus.RETURN_CONFIRMED,
            customerId,
            cancellationToken);

        await CompleteTripAsync(customerId, trip.Id, cancellationToken);
        await EvaluateSubmittedRatingAsync(
            trip.Id,
            customerId,
            trip.DriverId,
            cancellationToken);

        await CleanupTripTrackingAsync(trip.Id, cancellationToken);
    }

    public async Task ConfirmReturnByDriverAsync(
        Guid driverId,
        long tripId,
        IReadOnlyList<ReturnEvidenceItem> evidence,
        string? note,
        CancellationToken cancellationToken)
    {
        // Evidence count guard: 1–3 photos required (mirrors DB CHECK constraint on DisplayOrder).
        if (evidence.Count < 1 || evidence.Count > 3)
        {
            throw new BookingException(
                "trip.return_evidence_invalid_count",
                "Cần cung cấp từ 1 đến 3 ảnh bằng chứng bàn giao xe.",
                400);
        }

        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .Include(x => x.Booking)
                .ThenInclude(x => x.PricingRule)
            .Include(x => x.Booking)
                .ThenInclude(x => x.SurgePricingRule)
            .Include(x => x.Payments)
            .Include(x => x.ReturnConfirmations)
            .FirstOrDefaultAsync(
                x => x.Id == tripId && x.DriverId == driverId,
                cancellationToken);
        if (trip is null)
        {
            throw new BookingException(
                "trip.not_found",
                "Không tìm thấy chuyến đi của tài xế.",
                404);
        }

        var existingDriverConfirmation = trip.ReturnConfirmations
            .OrderByDescending(x => x.ConfirmedAt)
            .FirstOrDefault(x =>
                x.ConfirmedByUserId == driverId
                && x.HandoverStatus == HandoverStatus.DriverConfirmed);
        if ((trip.TripStatus is TripStatus.RETURN_CONFIRMED or TripStatus.COMPLETED)
            && existingDriverConfirmation is not null)
        {
            if (trip.TripStatus == TripStatus.RETURN_CONFIRMED)
            {
                await CompleteTripAsync(driverId, trip.Id, cancellationToken);
            }

            await CleanupTripTrackingAsync(trip.Id, cancellationToken);
            return;
        }

        if (trip.TripStatus != TripStatus.WAITING_RETURN_CONFIRM)
        {
            throw new BookingException(
                "trip.invalid_status_transition",
                "Chỉ có thể xác nhận trả xe thay khách khi chuyến đang chờ xác nhận.",
                409);
        }

        await EnsurePaymentSucceededAsync(trip, cancellationToken);

        var validatedFiles = new List<ValidatedEvidenceFile>(evidence.Count);
        try
        {
            foreach (var item in evidence)
            {
                validatedFiles.Add(await _evidenceFileValidator.ValidateAsync(
                    new EvidenceFileValidationRequest(
                        item.FileName,
                        item.ContentType,
                        item.SizeBytes,
                        item.Content,
                        ReturnEvidenceContentTypes,
                        10 * 1024 * 1024,
                        new EvidenceFileValidationErrorCodes(
                            "trip.return_evidence_invalid",
                            "trip.return_evidence_malware_detected",
                            "trip.return_evidence_scanner_unavailable")),
                    cancellationToken));
            }
        }
        catch
        {
            foreach (var file in validatedFiles) await file.Content.DisposeAsync();
            throw;
        }

        var storedFiles = new List<StoredReturnEvidenceFile>(evidence.Count);
        try
        {
            // Upload only after every file has passed validation and scanning.
            for (var i = 0; i < validatedFiles.Count; i++)
            {
                var item = validatedFiles[i];
                item.Content.Position = 0;
                var stored = await _tripReturnEvidenceStorage.SaveAsync(
                    tripId,
                    displayOrder: i + 1,
                    item.FileName,
                    item.ContentType,
                    item.Content,
                    cancellationToken);
                storedFiles.Add(stored);
            }

            if (!trip.EndedAt.HasValue || !trip.FinalFare.HasValue)
            {
                await FinalizeTripAsync(
                    trip,
                    driverId,
                    cancellationToken,
                    trip.EndReason ?? TripEndReason.NORMAL_COMPLETION);
            }

            // GPS is read from the server-side Redis cache; the driver cannot inject coordinates.
            decimal? capturedLatitude = null;
            decimal? capturedLongitude = null;
            var locationJson = await _redisService.GetAsync(RedisKeys.DriverLocation(driverId));
            if (locationJson is not null)
            {
                var locationCache = JsonSerializer.Deserialize<DriverLocationCache>(locationJson);
                if (locationCache is not null)
                {
                    capturedLatitude = (decimal)locationCache.Latitude;
                    capturedLongitude = (decimal)locationCache.Longitude;
                }
            }

            var utcNow = _dateTimeProvider.UtcNow;

            var confirmation = new Domain.Entities.TripReturnConfirmation
            {
                TripId = trip.Id,
                DriverId = driverId,
                ConfirmedByUserId = driverId,   // driver acted on behalf of customer
                HandoverStatus = HandoverStatus.DriverConfirmed,
                ConfirmedAt = utcNow,
                DriverLatitude = capturedLatitude,
                DriverLongitude = capturedLongitude,
                Note = note,
                CreatedAt = utcNow
            };

            for (var i = 0; i < storedFiles.Count; i++)
            {
                var sf = storedFiles[i];
                confirmation.Evidence.Add(new Domain.Entities.TripReturnEvidence
                {
                    ImageUrl = sf.ImageUrl,
                    ImagePublicId = sf.ImagePublicId,
                    OriginalFileName = sf.OriginalFileName,
                    ContentType = sf.ContentType,
                    FileSizeBytes = sf.FileSizeBytes,
                    DisplayOrder = i + 1,
                    CreatedAt = utcNow
                });
            }

            _dbContext.TripReturnConfirmations.Add(confirmation);

            // ApplyTripStatusAsync calls SaveChangesAsync, so the confirmation is persisted atomically.
            await ApplyTripStatusAsync(
                trip,
                TripStatus.RETURN_CONFIRMED,
                driverId,
                cancellationToken);

            await CompleteTripAsync(driverId, trip.Id, cancellationToken);

            await CleanupTripTrackingAsync(trip.Id, cancellationToken);
        }
        catch
        {
            var confirmationPersisted = await _dbContext.TripReturnConfirmations
                .AsNoTracking()
                .AnyAsync(
                    x => x.TripId == tripId
                        && x.ConfirmedByUserId == driverId
                        && x.HandoverStatus == HandoverStatus.DriverConfirmed,
                    CancellationToken.None);
            foreach (var stored in storedFiles.Where(x =>
                !confirmationPersisted
                && !string.IsNullOrWhiteSpace(x.ImagePublicId)))
            {
                try
                {
                    await _tripReturnEvidenceStorage.DeleteAsync(
                        stored.ImagePublicId!, CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    _logger.LogWarning(
                        cleanupException,
                        "Could not delete orphaned return evidence {PublicId} for trip {TripId}.",
                        stored.ImagePublicId,
                        tripId);
                }
            }
            throw;
        }
        finally
        {
            foreach (var file in validatedFiles) await file.Content.DisposeAsync();
        }
    }

    private static readonly string[] ReturnEvidenceContentTypes =
        ["image/jpeg", "image/png", "image/webp"];

    private async Task EvaluateSubmittedRatingAsync(
        long tripId,
        Guid customerId,
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var submittedRatingId = await _dbContext.Ratings
            .AsNoTracking()
            .Where(x =>
                x.TripId == tripId
                && x.CustomerId == customerId
                && x.DriverId == driverId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (submittedRatingId.HasValue)
        {
            await _accountBanEvaluationService.EvaluateRatingAsync(
                submittedRatingId.Value,
                cancellationToken);
        }
    }

    public Task SafetyTerminateAsync(
        Guid userId,
        bool isStaff,
        long tripId,
        string reason,
        CancellationToken cancellationToken) =>
        SafetyTerminateAsync(userId, isStaff, tripId, reason, evidence: [], cancellationToken);

    public async Task EnsureCanSafetyTerminateAsync(
        Guid userId,
        bool isStaff,
        long tripId,
        string reason,
        CancellationToken cancellationToken)
    {
        var normalizedReason = ValidateSafetyTerminationReason(reason);
        var trip = await _dbContext.Trips
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == tripId, cancellationToken);
        if (trip is null || !isStaff && trip.DriverId != userId)
            throw new BookingException("trip.not_found", "Không tìm thấy chuyến đi.", 404);
        if (trip.TripStatus == TripStatus.CANCELLED
            && trip.TerminationCategory == TripTerminationCategory.SAFETY
            && string.Equals(trip.SafetyTerminationReason, normalizedReason, StringComparison.Ordinal))
            return;
        if (trip.TripStatus is TripStatus.COMPLETED or TripStatus.CANCELLED
            || !CanTransition(trip.TripStatus, TripStatus.CANCELLED))
            throw new BookingException(
                "trip.safety_termination_invalid_status",
                "Chuyến đi đã kết thúc.",
                409);
    }

    public async Task SafetyTerminateAsync(
        Guid userId,
        bool isStaff,
        long tripId,
        string reason,
        IReadOnlyList<StoredSafetyTerminationEvidence> evidence,
        CancellationToken cancellationToken)
    {
        var ownsTransaction = _dbContext.Database.IsRelational()
            && _dbContext.Database.CurrentTransaction is null;
        if (!ownsTransaction)
        {
            await SafetyTerminateCoreAsync(userId, isStaff, tripId, reason, evidence, cancellationToken);
            return;
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            await SafetyTerminateCoreAsync(userId, isStaff, tripId, reason, evidence, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private async Task SafetyTerminateCoreAsync(
        Guid userId,
        bool isStaff,
        long tripId,
        string reason,
        IReadOnlyList<StoredSafetyTerminationEvidence> evidence,
        CancellationToken cancellationToken)
    {
        var normalizedReason = ValidateSafetyTerminationReason(reason);

        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .Include(x => x.Booking).ThenInclude(x => x.PricingRule)
            .Include(x => x.Booking).ThenInclude(x => x.SurgePricingRule)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == tripId, cancellationToken);
        if (trip is null || !isStaff && trip.DriverId != userId)
            throw new BookingException("trip.not_found", "Không tìm thấy chuyến đi.", 404);
        if (trip.TripStatus == TripStatus.CANCELLED
            && trip.TerminationCategory == TripTerminationCategory.SAFETY
            && string.Equals(trip.SafetyTerminationReason, normalizedReason, StringComparison.Ordinal))
            return;
        if (trip.TripStatus is TripStatus.COMPLETED or TripStatus.CANCELLED
            || !CanTransition(trip.TripStatus, TripStatus.CANCELLED))
            throw new BookingException(
                "trip.safety_termination_invalid_status",
                "Chuyến đi đã kết thúc.",
                409);

        if (trip.TripStatus == TripStatus.IN_PROGRESS)
        {
            await FinalizeTripAsync(
                trip,
                userId,
                cancellationToken,
                TripEndReason.SAFETY_TERMINATION);
            // Promotions are not applied or consumed for an MVP safety termination.
            trip.FinalFare = trip.ActualFare;
        }
        else
        {
            trip.ActualDistanceKm = null;
            trip.ActualDurationMinutes = null;
            trip.ActualFare = null;
            trip.FinalFare = null;
        }

        trip.TerminationCategory = TripTerminationCategory.SAFETY;
        trip.EndReason = TripEndReason.SAFETY_TERMINATION;
        trip.SafetyTerminationReason = normalizedReason;
        trip.SafetyTerminatedAt = _dateTimeProvider.UtcNow;
        trip.CancellationReason = normalizedReason;
        foreach (var pendingPayment in trip.Payments.Where(x => x.PaymentStatus == PaymentStatus.Pending))
        {
            pendingPayment.PaymentStatus = PaymentStatus.Cancelled;
            pendingPayment.UpdatedAt = _dateTimeProvider.UtcNow;
        }
        foreach (var item in evidence)
        {
            _dbContext.SafetyTerminationEvidence.Add(new Domain.Entities.SafetyTerminationEvidence
            {
                TripId = trip.Id,
                EvidenceUrl = item.EvidenceUrl,
                StoragePublicId = item.StoragePublicId,
                OriginalFileName = item.OriginalFileName,
                ContentType = item.ContentType,
                FileSizeBytes = item.FileSizeBytes,
                UploadedByUserId = userId,
                CreatedAtUtc = _dateTimeProvider.UtcNow
            });
        }
        await ApplyTripStatusAsync(trip, TripStatus.CANCELLED, userId, cancellationToken);
        await _safetyPaymentReconciliationService.ReconcileAsync(trip, cancellationToken);
        await CleanupTripTrackingAsync(trip.Id, cancellationToken);
    }

    private static string ValidateSafetyTerminationReason(string reason)
    {
        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason))
            throw new BookingException(
                "trip.safety_reason_required",
                "Lý do kết thúc vì an toàn là bắt buộc.",
                400);
        if (normalizedReason.Length > 500)
            throw new BookingException(
                "trip.safety_reason_too_long",
                "Lý do kết thúc vì an toàn không được vượt quá 500 ký tự.",
                400);
        return normalizedReason;
    }

    private async Task ApplyTripStatusAsync(
        Domain.Entities.Trip trip,
        TripStatus tripStatus,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        if (!CanTransition(trip.TripStatus, tripStatus))
        {
            throw new BookingException(
                "trip.invalid_status_transition",
                "Trang thai chuyen di khong hop le.",
                409);
        }

        var utcNow = _dateTimeProvider.UtcNow;
        var previousTripStatus = trip.TripStatus;
        var previousBookingStatus = trip.Booking.BookingStatus;
        Domain.Entities.Payment? pendingPaymentNotification = null;
        if (tripStatus == TripStatus.IN_PROGRESS && previousTripStatus != TripStatus.IN_PROGRESS)
        {
            await _preTripVehicleCheckService.EnsureCanStartAndActivateCoverageAsync(
                trip.DriverId,
                trip,
                utcNow,
                cancellationToken);
        }
        if (tripStatus == TripStatus.COMPLETED)
        {
            var successfulPayment = trip.Payments
                .OrderByDescending(x => x.PaidAt)
                .FirstOrDefault(x => x.PaymentStatus == PaymentStatus.Success);
            var settlement = await _financialSettlementService.GetOrCreateAsync(
                trip,
                safetyTerminated: false,
                cancellationToken);
            if (successfulPayment?.PaymentMethod == PaymentMethod.QR)
            {
                await _tripPaymentSettlementService.SettleSuccessfulQrPaymentAsync(
                    trip,
                    successfulPayment.TransactionReference,
                    cancellationToken);
            }
            else if (successfulPayment?.PaymentMethod == PaymentMethod.CASH)
            {
                await _financialSettlementService.ApplyCashWalletAdjustmentAsync(
                    trip,
                    cancellationToken);
            }
            else if (settlement.CustomerPayableAmount == 0m)
            {
                await _financialSettlementService.SettleQrDriverEarningAsync(
                    trip,
                    providerReference: "PLATFORM_PROMOTION",
                    cancellationToken);
            }
        }
        trip.TripStatus = tripStatus;
        // Flow: state machine stamps milestone times; terminal states settle promotion/driver/cache state.
        switch (tripStatus)
        {
            case TripStatus.ARRIVED:
                trip.ArrivedAt ??= utcNow;
                break;
            case TripStatus.IN_PROGRESS:
                trip.StartedAt ??= utcNow;
                break;
            case TripStatus.WAITING_RETURN_CONFIRM:
                trip.StartedAt ??= utcNow;
                break;
            case TripStatus.WAITING_PAYMENT:
                trip.StartedAt ??= utcNow;
                EnsureTripFare(trip);
                pendingPaymentNotification = UpsertPendingPayment(trip, utcNow);
                break;
            case TripStatus.COMPLETED:
                trip.StartedAt ??= utcNow;
                trip.CompletedAt ??= utcNow;
                EnsureTripFare(trip);
                trip.Booking.BookingStatus = BookingStatus.Completed;
                trip.Booking.UpdatedAt = utcNow;
                if (previousTripStatus != TripStatus.COMPLETED)
                {
                    IncrementPromotionUsage(trip.Booking);
                }
                await ReleaseDriverAsync(trip.DriverId, utcNow, cancellationToken);
                await _financialSettlementService.CreateContributionForCompletedTripAsync(
                    trip,
                    cancellationToken);
                break;
            case TripStatus.CANCELLED:
                trip.CancelledByUserId = changedByUserId;
                trip.Booking.BookingStatus = BookingStatus.Cancelled;
                trip.Booking.UpdatedAt = utcNow;
                if (previousTripStatus != TripStatus.COMPLETED &&
                    previousBookingStatus != BookingStatus.Completed)
                {
                    RemoveBookingPromotions(trip.Booking);
                }
                await ReleaseDriverAsync(trip.DriverId, utcNow, cancellationToken);
                break;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _tripSharingService.HandleTripLifecycleAsync(
            trip.Id,
            tripStatus,
            utcNow,
            cancellationToken);
        // Flow: keep live trip cache only for active trips; terminal trips publish final booking status too.
        if (tripStatus is TripStatus.COMPLETED or TripStatus.CANCELLED)
        {
            await _redisService.RemoveAsync(RedisKeys.TripLive(trip.Id));
            await _redisService.RemoveAsync(RedisKeys.DriverActiveTrip(trip.DriverId));
            await _redisService.ExpireAsync(
                RedisKeys.TripChatMessages(trip.Id),
                TimeSpan.FromHours(2),
                cancellationToken);
        }
        else
        {
            await CacheTripLiveAsync(trip, utcNow);
            if (tripStatus == TripStatus.IN_PROGRESS
                && previousTripStatus != TripStatus.IN_PROGRESS)
            {
                await RecordCurrentDriverLocationForTripAsync(
                    trip,
                    utcNow,
                    cancellationToken);
            }
        }

        await _realtimeNotificationService.PublishTripStatusChangedAsync(
            new TripStatusChangedEvent(
                trip.Id,
                trip.BookingId,
                trip.Booking.CustomerId,
                trip.DriverId,
                trip.TripStatus,
                utcNow,
                trip.Booking.BookingStatus),
            cancellationToken);

        if (tripStatus == TripStatus.WAITING_PAYMENT
            && previousTripStatus != TripStatus.WAITING_PAYMENT
            && pendingPaymentNotification is not null)
        {
            await _realtimeNotificationService.PublishTripPaymentPendingAsync(
                new TripPaymentPendingEvent(
                    trip.Id,
                    trip.BookingId,
                    trip.Booking.CustomerId,
                    trip.DriverId,
                    pendingPaymentNotification.Id,
                    pendingPaymentNotification.PaymentMethod,
                    pendingPaymentNotification.PaymentStatus,
                    pendingPaymentNotification.Amount,
                    pendingPaymentNotification.Currency,
                    trip.TripStatus,
                    pendingPaymentNotification.CreatedAt,
                    "Vui lòng thanh toán cho tài xế để hoàn tất chuyến đi.",
                    trip.Booking.BookingStatus),
                cancellationToken);
        }

        if (tripStatus is TripStatus.COMPLETED or TripStatus.CANCELLED)
        {
            await _realtimeNotificationService.PublishBookingStatusChangedAsync(
                new BookingStatusChangedEvent(
                    trip.BookingId,
                    trip.Booking.CustomerId,
                    trip.Booking.BookingStatus,
                    utcNow),
                cancellationToken);
        }
    }

    private async Task CacheTripLiveAsync(
        Domain.Entities.Trip trip,
        DateTime utcNow)
    {
        var assignedAt = trip.DriverAssignedAt ?? utcNow;
        var cache = new TripLiveCache(
            trip.Id,
            trip.BookingId,
            trip.DriverId,
            trip.Booking.CustomerId,
            trip.TripStatus,
            assignedAt);
        var driverActiveTrip = new DriverActiveTripCache(
            trip.Id,
            trip.BookingId,
            trip.DriverId,
            trip.Booking.CustomerId,
            trip.TripStatus,
            assignedAt,
            trip.Booking.RoutePolyline,
            trip.Booking.DestinationLocation?.Y,
            trip.Booking.DestinationLocation?.X);

        await _redisService.SetAsync(
            RedisKeys.TripLive(trip.Id),
            JsonSerializer.Serialize(cache),
            TimeSpan.FromHours(_options.CurrentValue.TripLiveTtlHours));
        await _redisService.SetAsync(
            RedisKeys.DriverActiveTrip(trip.DriverId),
            JsonSerializer.Serialize(driverActiveTrip),
            TimeSpan.FromHours(_options.CurrentValue.TripLiveTtlHours));
    }

    private async Task RecordCurrentDriverLocationForTripAsync(
        Domain.Entities.Trip trip,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        try
        {
            var locationJson = await _redisService.GetAsync(
                RedisKeys.DriverLocation(trip.DriverId));
            if (string.IsNullOrWhiteSpace(locationJson))
            {
                return;
            }

            var location = JsonSerializer.Deserialize<DriverLocationCache>(locationJson);
            if (location is null)
            {
                return;
            }

            var timestampUnixMs = new DateTimeOffset(utcNow).ToUnixTimeMilliseconds();
            var point = new TripTrackingPoint(
                trip.Id,
                location.Latitude,
                location.Longitude,
                timestampUnixMs,
                timestampUnixMs,
                utcNow);
            var options = _options.CurrentValue;
            var writeOptions = new TripTrackingWriteOptions(
                TimeSpan.FromHours(options.TrackingTtlHours),
                options.MaxPathPoints,
                options.AccumulatorJitterThresholdMeters,
                options.PathSampleDistanceMeters,
                options.PathSampleIntervalSeconds,
                options.MaxInferredSpeedKmh,
                options.MaxAccuracyMeters);

            await _redisService.RecordTripTrackingPointAsync(
                point,
                writeOptions,
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to record cached driver location for trip {TripId}. Trip status update will continue.",
                trip.Id);
        }
    }

    private async Task FinalizeActualTripAsync(
        Domain.Entities.Trip trip,
        DateTime endedAtUtc,
        CancellationToken cancellationToken,
        TripEndReason reason)
    {
        if (trip.EndedAt.HasValue
            && trip.ActualDistanceKm.HasValue
            && trip.ActualDurationMinutes.HasValue
            && trip.ActualFare.HasValue
            && trip.FinalFare.HasValue)
        {
            return;
        }

        var snapshot = await _redisService.GetTripTrackingSnapshotAsync(
            trip.Id,
            cancellationToken);
        trip.EndedAt ??= endedAtUtc;
        trip.ActualDurationMinutes ??= CalculateActualDurationMinutes(
            trip.StartedAt ?? snapshot.TrackingStartedAtUtc ?? endedAtUtc,
            endedAtUtc);

        var routeEstimate = await TryGetFallbackRouteEstimateAsync(
            snapshot,
            cancellationToken);
        trip.ActualDistanceKm ??= ResolveActualDistanceKm(
            trip,
            snapshot,
            routeEstimate);
        trip.RoutePolyline = ResolveActualPolyline(snapshot, routeEstimate);
        trip.FareFinalizedAtUtc ??= endedAtUtc;
        if (snapshot.LastAcceptedPoint is not null)
        {
            trip.FinalizationLatitude ??= decimal.Round(
                (decimal)snapshot.LastAcceptedPoint.Latitude,
                6,
                MidpointRounding.AwayFromZero);
            trip.FinalizationLongitude ??= decimal.Round(
                (decimal)snapshot.LastAcceptedPoint.Longitude,
                6,
                MidpointRounding.AwayFromZero);
        }

        TripFareFinalizationResult fare;
        if (trip.Booking.PricingSnapshotVersion is >= Domain.Entities.Booking.CurrentPricingSnapshotVersion
            && reason is not TripEndReason.SAFETY_TERMINATION
                and not TripEndReason.VEHICLE_SAFETY_ISSUE)
        {
            var isHourlyBooking = trip.Booking.AcceptedPricePerHour is > 0m
                && !trip.Booking.AcceptedPricePerKm.HasValue;
            var destinationReached = !isHourlyBooking && IsDestinationReached(
                snapshot.LastAcceptedPoint,
                trip.Booking.DestinationLocation?.Y,
                trip.Booking.DestinationLocation?.X);
            var plannedProgress = await ResolvePlannedRouteProgressAsync(
                trip,
                snapshot.LastAcceptedPoint);
            trip.TerminationCategory = TripTerminationCategory.STANDARD;
            trip.EndReason = reason;
            trip.DestinationReached = isHourlyBooking ? null : destinationReached;
            trip.PlannedRouteProgress = plannedProgress;
            fare = _tripFareFinalizationService.CalculateLockedFare(
                trip,
                reason,
                plannedProgress,
                destinationReached);
        }
        else
        {
            // V0 trips stay on the isolated compatibility calculator. Safety
            // termination also preserves the existing Risk Protection payable
            // input before its dedicated reconciliation runs.
            if (reason is not TripEndReason.SAFETY_TERMINATION
                and not TripEndReason.VEHICLE_SAFETY_ISSUE)
            {
                trip.TerminationCategory = TripTerminationCategory.STANDARD;
                trip.EndReason = reason;
                trip.DestinationReached = IsDestinationReached(
                    snapshot.LastAcceptedPoint,
                    trip.Booking.DestinationLocation?.Y,
                    trip.Booking.DestinationLocation?.X);
                trip.PlannedRouteProgress = await ResolvePlannedRouteProgressAsync(
                    trip,
                    snapshot.LastAcceptedPoint);
            }
            fare = _tripFareFinalizationService.Calculate(
                trip,
                trip.ActualDistanceKm.Value,
                trip.ActualDurationMinutes.Value);
        }
        trip.ActualFare = fare.ActualFare;
        trip.FinalFare = fare.FinalFare;
    }

    private async Task FinalizeTripAsync(
        Domain.Entities.Trip trip,
        Guid actorId,
        CancellationToken cancellationToken,
        TripEndReason reason)
    {
        var lockAcquired = await _redisService.TryAcquireDistributedLockAsync(
            RedisKeys.TripTrackingFinalizeLock(trip.Id),
            $"{actorId:N}:{Guid.NewGuid():N}",
            TimeSpan.FromSeconds(_options.CurrentValue.FinalizeLockSeconds));
        if (!lockAcquired)
        {
            throw new BookingException(
                "trip.finalization_in_progress",
                "Chuyến đi đang được kết thúc. Vui lòng thử lại sau.",
                409);
        }

        var utcNow = _dateTimeProvider.UtcNow;
        await RecordCurrentDriverLocationForTripAsync(
            trip,
            utcNow,
            cancellationToken);
        await FinalizeActualTripAsync(
            trip,
            utcNow,
            cancellationToken,
            reason);
    }

    private async Task<decimal> ResolvePlannedRouteProgressAsync(
        Domain.Entities.Trip trip,
        TripTrackingPoint? lastPoint)
    {
        var maximumProgress = 0d;
        var progressJson = await _redisService.GetAsync(
            RedisKeys.TripPlannedRouteProgress(trip.Id));
        if (!string.IsNullOrWhiteSpace(progressJson))
        {
            if (double.TryParse(
                progressJson,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var cachedProgress))
            {
                maximumProgress = cachedProgress;
            }
            else
            {
                await _redisService.RemoveAsync(
                    RedisKeys.TripPlannedRouteProgress(trip.Id));
            }
        }

        if (lastPoint is not null && !string.IsNullOrWhiteSpace(trip.Booking.RoutePolyline))
        {
            try
            {
                var route = EncodedPolylineGeometry.Decode(trip.Booking.RoutePolyline);
                var projection = EncodedPolylineGeometry.Project(
                    new LocationPoint(lastPoint.Latitude, lastPoint.Longitude),
                    route);
                if (projection.TotalRouteMeters > 0
                    && projection.DistanceToRouteMeters
                        <= _options.CurrentValue.RouteDeviationThresholdMeters)
                {
                    maximumProgress = Math.Max(
                        maximumProgress,
                        projection.ProgressMeters / projection.TotalRouteMeters);
                }
            }
            catch (FormatException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Invalid original booking route while finalizing trip {TripId}.",
                    trip.Id);
            }
        }

        return decimal.Round(
            (decimal)Math.Clamp(maximumProgress, 0d, 1d),
            6,
            MidpointRounding.AwayFromZero);
    }

    private bool IsDestinationReached(
        TripTrackingPoint? lastPoint,
        double? destinationLatitude,
        double? destinationLongitude)
    {
        if (lastPoint is null
            || !destinationLatitude.HasValue
            || !destinationLongitude.HasValue)
        {
            return false;
        }

        return _tripFareFinalizationService.IsDestinationReached(
                lastPoint.Latitude,
                lastPoint.Longitude,
                destinationLatitude.Value,
                destinationLongitude.Value);
    }

    private static void ValidateDriverEndReason(TripEndReason reason)
    {
        if (reason is TripEndReason.NORMAL_COMPLETION
            or TripEndReason.CUSTOMER_REQUESTED_STOP)
        {
            return;
        }

        if (reason is TripEndReason.DRIVER_UNABLE_TO_CONTINUE
            or TripEndReason.STARTED_BY_MISTAKE)
        {
            throw new BookingException(
                "trip.end_reconciliation_required",
                "This end reason must be submitted for staff reconciliation.",
                409);
        }

        if (reason is TripEndReason.VEHICLE_SAFETY_ISSUE
            or TripEndReason.SAFETY_TERMINATION)
        {
            throw new BookingException(
                "trip.safety_termination_required",
                "Hãy dùng quy trình kết thúc vì an toàn để Risk Protection xử lý chuyến đi.",
                409);
        }

        throw new BookingException(
            "trip.end_reason_not_allowed",
            "Tài xế không được phép sử dụng lý do kết thúc chuyến này.",
            403);
    }

    private async Task<RouteEstimateResult?> TryGetFallbackRouteEstimateAsync(
        TripTrackingSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot.FirstAcceptedPoint is null
            || snapshot.LastAcceptedPoint is null
            || PointsAreSame(snapshot.FirstAcceptedPoint, snapshot.LastAcceptedPoint))
        {
            return null;
        }

        if (snapshot.DistanceMeters >= _options.CurrentValue.MinTrustedDistanceMeters
            && snapshot.PathPoints.Count >= _options.CurrentValue.MinFallbackPathPointCount)
        {
            return null;
        }

        try
        {
            return await _mapRoutingService.GetRouteEstimateAsync(
                new RouteEstimateRequest
                {
                    Origin = new LocationPoint(
                        snapshot.FirstAcceptedPoint.Latitude,
                        snapshot.FirstAcceptedPoint.Longitude),
                    Destination = new LocationPoint(
                        snapshot.LastAcceptedPoint.Latitude,
                        snapshot.LastAcceptedPoint.Longitude),
                    Provider = MapProvider.Auto,
                    TravelMode = MapTravelMode.Car,
                    IncludePolyline = true,
                    RequestSource = "TripFinalization"
                },
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to calculate fallback route for trip finalization.");
            return null;
        }
    }

    private decimal ResolveActualDistanceKm(
        Domain.Entities.Trip trip,
        TripTrackingSnapshot snapshot,
        RouteEstimateResult? routeEstimate)
    {
        if (snapshot.DistanceMeters >= _options.CurrentValue.MinTrustedDistanceMeters)
        {
            return decimal.Round(
                (decimal)(snapshot.DistanceMeters / 1000d),
                2,
                MidpointRounding.AwayFromZero);
        }

        if (routeEstimate is not null)
        {
            return decimal.Round(
                (decimal)routeEstimate.DistanceKm,
                2,
                MidpointRounding.AwayFromZero);
        }

        return decimal.Round(
            (decimal)(snapshot.DistanceMeters / 1000d),
            2,
            MidpointRounding.AwayFromZero);
    }

    private string? ResolveActualPolyline(
        TripTrackingSnapshot snapshot,
        RouteEstimateResult? routeEstimate)
    {
        var pathPoints = BuildFinalPolylinePoints(snapshot);
        if (pathPoints.Count >= _options.CurrentValue.MinFallbackPathPointCount)
        {
            return TripPathPolylineEncoder.Encode(pathPoints);
        }

        return routeEstimate?.EncodedPolyline;
    }

    private static List<TripTrackingPoint> BuildFinalPolylinePoints(
        TripTrackingSnapshot snapshot)
    {
        var points = snapshot.PathPoints.ToList();
        if (snapshot.LastAcceptedPoint is not null
            && (points.Count == 0
                || !PointsAreSame(points[^1], snapshot.LastAcceptedPoint)))
        {
            points.Add(snapshot.LastAcceptedPoint);
        }

        return points;
    }

    private async Task CleanupTripTrackingAsync(
        long tripId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _redisService.RemoveTripTrackingAsync(tripId, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to cleanup Redis trip tracking keys for trip {TripId}; TTL will expire them.",
                tripId);
        }
    }

    private async Task ReleaseDriverAsync(
        Guid driverId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.DriverProfiles
            .FirstOrDefaultAsync(x => x.DriverId == driverId, cancellationToken);
        if (profile is not null)
        {
            profile.WorkStatus = DriverWorkStatus.Online;
            profile.LastActiveAt = utcNow;
            profile.UpdatedAt = utcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        await _redisService.SetAsync(
            RedisKeys.DriverOnline(driverId),
            "1",
            TimeSpan.FromMinutes(_options.CurrentValue.DriverStatusTtlMinutes));
        await _redisService.SetAsync(
            RedisKeys.DriverStatus(driverId),
            DriverWorkStatus.Online.ToString(),
            TimeSpan.FromMinutes(_options.CurrentValue.DriverStatusTtlMinutes));
        await _redisService.RemoveAsync(RedisKeys.DriverActiveTrip(driverId));
    }

    private static void IncrementPromotionUsage(Domain.Entities.Booking booking)
    {
        foreach (var bookingPromotion in booking.BookingPromotions)
        {
            bookingPromotion.Promotion.CurrentUsageCount += 1;
        }
    }

    private static void EnsureTripFare(Domain.Entities.Trip trip)
    {
        if (trip.ActualFare != null && trip.FinalFare != null)
        {
            return;
        }

        trip.ActualFare ??= trip.Booking.EstimatedFare;
        var discountAmount = trip.Booking.BookingPromotions.Sum(x => x.DiscountAmount);
        trip.FinalFare ??= decimal.Round(
            Math.Max(0m, trip.ActualFare.Value - discountAmount),
            0,
            MidpointRounding.AwayFromZero);
    }

    private static Domain.Entities.Payment? UpsertPendingPayment(
        Domain.Entities.Trip trip,
        DateTime utcNow)
    {
        var amount = trip.FinalFare ?? trip.ActualFare ?? trip.Booking.EstimatedFare;
        if (amount <= 0m)
        {
            return null;
        }

        var existingSuccess = trip.Payments
            .Any(payment => payment.PaymentStatus == PaymentStatus.Success);
        if (existingSuccess)
        {
            return null;
        }

        var pending = trip.Payments
            .OrderByDescending(payment => payment.CreatedAt)
            .FirstOrDefault(payment => payment.PaymentStatus == PaymentStatus.Pending);
        if (pending is not null)
        {
            if (pending.PaymentMethod == PaymentMethod.QR
                && !string.IsNullOrWhiteSpace(pending.TransactionReference))
            {
                // A pre-trip QR order is an immutable provider intent. Its paid
                // amount is reconciled against the final settlement later.
                return pending;
            }

            pending.PaymentMethod = PaymentMethod.CASH;
            pending.TransactionReference = null;
            pending.Amount = amount;
            pending.Currency = "VND";
            pending.UpdatedAt = utcNow;
            return pending;
        }

        var payment = new Domain.Entities.Payment
        {
            TripId = trip.Id,
            PaymentMethod = PaymentMethod.CASH,
            TransactionReference = null,
            Amount = amount,
            Currency = "VND",
            PaymentStatus = PaymentStatus.Pending,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
        trip.Payments.Add(payment);
        return payment;
    }

    private async Task EnsurePaymentSucceededAsync(
        Domain.Entities.Trip trip,
        CancellationToken cancellationToken)
    {
        if (trip.Payments.Any(payment => payment.PaymentStatus == PaymentStatus.Success))
        {
            var reconciliation = await _safetyPaymentReconciliationService.ReconcileAsync(
                trip,
                cancellationToken);
            if (reconciliation.RemainingPayableAmount == 0m)
            {
                return;
            }
        }
        if (await _dbContext.TripFinancialSettlements.AnyAsync(
                settlement => settlement.TripId == trip.Id
                    && settlement.CustomerPayableAmount == 0
                    && settlement.SettledAtUtc != null,
                cancellationToken))
        {
            return;
        }

        throw new BookingException(
            "payment.required_before_return_confirmation",
            "Vui lòng hoàn tất thanh toán trước khi xác nhận trả xe.",
            409);
    }

    private async Task<bool> HasPaymentSucceededAsync(
        Domain.Entities.Trip trip,
        CancellationToken cancellationToken)
    {
        if (trip.Payments.Any(payment => payment.PaymentStatus == PaymentStatus.Success))
        {
            var reconciliation = await _safetyPaymentReconciliationService.ReconcileAsync(
                trip,
                cancellationToken);
            return reconciliation.RemainingPayableAmount == 0m;
        }

        return await _dbContext.TripFinancialSettlements.AnyAsync(
            settlement => settlement.TripId == trip.Id
                && settlement.CustomerPayableAmount == 0
                && settlement.SettledAtUtc != null,
            cancellationToken);
    }

    private void RemoveBookingPromotions(Domain.Entities.Booking booking)
    {
        if (booking.BookingPromotions.Count == 0)
        {
            return;
        }

        _dbContext.BookingPromotions.RemoveRange(booking.BookingPromotions);
    }

    private static bool CanTransition(
        TripStatus current,
        TripStatus requested)
    {
        if (current == requested)
        {
            return true;
        }

        return current switch
        {
            TripStatus.ACCEPTED => requested is TripStatus.DRIVER_ARRIVING
                or TripStatus.ARRIVED
                or TripStatus.CANCELLED,
            TripStatus.DRIVER_ARRIVING => requested is TripStatus.ARRIVED
                or TripStatus.CANCELLED,
            TripStatus.ARRIVED => requested is TripStatus.IN_PROGRESS
                or TripStatus.CANCELLED,
            TripStatus.IN_PROGRESS => requested is TripStatus.WAITING_PAYMENT
                or TripStatus.CANCELLED,
            TripStatus.WAITING_RETURN_CONFIRM => requested is TripStatus.RETURN_CONFIRMED,
            TripStatus.RETURN_CONFIRMED => requested is TripStatus.COMPLETED,
            TripStatus.WAITING_PAYMENT => requested is TripStatus.WAITING_RETURN_CONFIRM,
            _ => false
        };
    }

    private static bool IsTripCoverageUniqueConstraintViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current.Message.Contains(
                    "IX_TripProtectionCoverages_TripId",
                    StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains(
                    "TripProtectionCoverages.TripId",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEndReconciliationUniqueConstraintViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current.Message.Contains(
                    "UX_TripEndReconciliations_Trip_Pending",
                    StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("2601", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("2627", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static TripEndReconciliationResult ToEndReconciliationResult(
        Domain.Entities.TripEndReconciliationRequest request) =>
        new(
            request.Id,
            request.TripId,
            request.RequestedReason,
            request.Status,
            request.RequestedAtUtc,
            request.ResolvedAtUtc,
            request.Status == TripEndReconciliationStatus.PENDING
                ? "The request was submitted for staff review; fare has not been finalized."
                : request.Status == TripEndReconciliationStatus.APPROVED
                    ? "Staff approved and finalized the reconciled end reason."
                    : "Staff rejected the request; the trip remains active.");

    private static int CalculateActualDurationMinutes(
        DateTime startedAtUtc,
        DateTime endedAtUtc)
    {
        var minutes = (endedAtUtc - startedAtUtc).TotalMinutes;
        return Math.Max(0, (int)Math.Ceiling(minutes));
    }

    private static bool PointsAreSame(
        TripTrackingPoint first,
        TripTrackingPoint second)
    {
        return Math.Abs(first.Latitude - second.Latitude) < 0.000001
            && Math.Abs(first.Longitude - second.Longitude) < 0.000001;
    }
}
