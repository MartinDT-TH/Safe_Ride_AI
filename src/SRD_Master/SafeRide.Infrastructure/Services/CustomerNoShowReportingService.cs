using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Features.Bookings;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.Infrastructure.Services;

public sealed class CustomerNoShowReportingService : ICustomerNoShowReportingService
{
    private const string CancellationReason = "Khách không xuất hiện sau khi tài xế đã đến điểm đón.";
    private readonly ApplicationDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IOptionsMonitor<CustomerNoShowOptions> _options;
    private readonly IBookingLifecycleJobScheduler _jobScheduler;
    private readonly IRedisService _redisService;

    public CustomerNoShowReportingService(ApplicationDbContext dbContext, IDateTimeProvider dateTimeProvider,
        IOptionsMonitor<CustomerNoShowOptions> options, IBookingLifecycleJobScheduler jobScheduler,
        IRedisService redisService)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _options = options;
        _jobScheduler = jobScheduler;
        _redisService = redisService;
    }

    public async Task<CustomerNoShowReportResponse> ReportAsync(Guid driverId, long tripId, CancellationToken cancellationToken)
    {
        var ownsTransaction = _dbContext.Database.IsRelational() && _dbContext.Database.CurrentTransaction is null;
        if (!ownsTransaction)
            return await ReportCoreAsync(driverId, tripId, cancellationToken);

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        CustomerNoShowReportResponse? result = null;
        await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            result = await ReportCoreAsync(driverId, tripId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
        return result!;
    }

    private async Task<CustomerNoShowReportResponse> ReportCoreAsync(Guid driverId, long tripId, CancellationToken cancellationToken)
    {
        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
            .Include(x => x.Driver)
            .FirstOrDefaultAsync(x => x.Id == tripId, cancellationToken);
        if (trip is null)
            throw new BookingException("trip.not_found", "Không tìm thấy chuyến đi.", 404);
        if (trip.DriverId != driverId)
            throw new BookingException("trip.driver_not_assigned", "Tài xế không được phân công cho chuyến đi này.", 403);

        var existing = await _dbContext.CustomerBehaviorEvents
            .FirstOrDefaultAsync(x => x.TripId == tripId
                && x.EventType == CustomerBehaviorEventType.VERIFIED_NO_SHOW
                && x.Status != CustomerBehaviorEventStatus.REVERSED, cancellationToken);
        if (existing is not null)
            throw new BookingException("trip.customer_no_show_already_reported", "Chuyến đi đã được báo khách không xuất hiện.", 409);

        var now = _dateTimeProvider.UtcNow;
        var waitSatisfiedAt = trip.ArrivedAt?.AddMinutes(_options.CurrentValue.NoShowWaitMinutes);
        if (trip.TripStatus != TripStatus.ARRIVED || trip.ArrivedAt is null)
            throw new BookingException("trip.customer_no_show_not_arrived", "Chuyến đi chưa ở trạng thái đã đến điểm đón.", 400);
        if (trip.ArrivalLocationVerifiedAt is null)
            throw new BookingException("trip.customer_no_show_arrival_not_verified", "Chưa xác minh GPS tại điểm đón.", 400);
        if (trip.CustomerNoShowReminderSentAt is null)
            throw new BookingException("trip.customer_no_show_reminder_missing", "Chưa gửi nhắc nhở cho khách.", 400);
        if (waitSatisfiedAt > now)
            throw new BookingException("trip.customer_no_show_wait_incomplete", "Chưa đủ thời gian chờ khách.", 400);
        if (trip.StartedAt is not null)
            throw new BookingException("trip.customer_no_show_trip_started", "Chuyến đi đã bắt đầu.", 400);

        var behaviorEvent = new CustomerBehaviorEvent
        {
            CustomerId = trip.Booking.CustomerId,
            BookingId = trip.BookingId,
            TripId = trip.Id,
            EventType = CustomerBehaviorEventType.VERIFIED_NO_SHOW,
            Status = CustomerBehaviorEventStatus.VERIFIED,
            DriverId = driverId,
            DriverReportedAt = now,
            ArrivedAt = trip.ArrivedAt,
            ArrivalLatitude = trip.ArrivalLatitude,
            ArrivalLongitude = trip.ArrivalLongitude,
            ArrivalDistanceMeters = trip.ArrivalDistanceMeters,
            ReminderSentAt = trip.CustomerNoShowReminderSentAt,
            WaitSatisfiedAt = waitSatisfiedAt,
            VerifiedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        _dbContext.CustomerBehaviorEvents.Add(behaviorEvent);

        var acceptedOffer = await _dbContext.BookingDriverOffers
            .Where(x => x.BookingId == trip.BookingId
                && x.DriverId == driverId
                && x.OfferStatus == DriverOfferStatus.CustomerConfirmed)
            .OrderByDescending(x => x.ConfirmedAt)
            .FirstOrDefaultAsync(cancellationToken);
        var pickupDistanceKm = acceptedOffer?.PickupDistanceKm;
        var supportEligible = pickupDistanceKm.HasValue
            && pickupDistanceKm.Value > (decimal)_options.CurrentValue.DriverSupportMinPickupDistanceKm;
        var supportAmount = supportEligible ? _options.CurrentValue.DriverNoShowSupportAmount : 0m;
        DriverNoShowSupport? support = null;
        if (supportEligible)
        {
            var wallet = await _dbContext.DriverWallets
                .SingleOrDefaultAsync(x => x.DriverId == driverId, cancellationToken);
            if (wallet is null)
            {
                wallet = new DriverWallet { DriverId = driverId, CurrentBalance = 0m };
                _dbContext.DriverWallets.Add(wallet);
            }

            var walletTransaction = new WalletTransaction
            {
                Wallet = wallet,
                TripId = trip.Id,
                TransactionType = WalletTransactionType.Bonus,
                SettlementEffect = null,
                Amount = supportAmount,
                Description = $"Hỗ trợ no-show cho tài xế, chuyến #{trip.Id}.",
                CreatedAt = now
            };
            wallet.CurrentBalance += supportAmount;
            _dbContext.WalletTransactions.Add(walletTransaction);
            support = new DriverNoShowSupport
            {
                Trip = trip,
                Booking = trip.Booking,
                Driver = trip.Driver,
                CustomerBehaviorEvent = behaviorEvent,
                AcceptedPickupDistanceKm = pickupDistanceKm!.Value,
                SupportAmount = supportAmount,
                Status = DriverNoShowSupportStatus.CREDITED,
                WalletTransaction = walletTransaction,
                CreatedAt = now,
                PaidAt = now
            };
            _dbContext.DriverNoShowSupports.Add(support);
        }

        trip.TripStatus = TripStatus.CANCELLED;
        trip.CancellationReason = CancellationReason;
        trip.CancelledByUserId = driverId;
        trip.Booking.BookingStatus = BookingStatus.Cancelled;
        trip.Booking.CancellationReason = CancellationReason;
        trip.Booking.CancelledBy = driverId;
        trip.Booking.UpdatedAt = now;

        var offers = await _dbContext.BookingDriverOffers
            .Where(x => x.BookingId == trip.BookingId
                && (x.OfferStatus == DriverOfferStatus.Sent || x.OfferStatus == DriverOfferStatus.DriverAccepted))
            .ToListAsync(cancellationToken);
        foreach (var offer in offers) offer.OfferStatus = DriverOfferStatus.Cancelled;
        if (trip.Driver.WorkStatus == DriverWorkStatus.Busy)
            trip.Driver.WorkStatus = DriverWorkStatus.Online;
        trip.Driver.LastActiveAt = now;
        trip.Driver.UpdatedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _jobScheduler.CancelJobsForBookingAsync(trip.BookingId, cancellationToken);
        await _redisService.RemoveAsync(RedisKeys.DriverActiveTrip(driverId));
        await _redisService.RemoveAsync(RedisKeys.TripLive(trip.Id));
        await _redisService.SetAsync(RedisKeys.DriverStatus(driverId), DriverWorkStatus.Online.ToString(), TimeSpan.FromMinutes(5));

        var message = supportEligible
            ? "Đã ghi nhận khách không xuất hiện. Khách hàng không bị thu phí; hỗ trợ tài xế đã được cộng vào ví."
            : "Đã ghi nhận khách không xuất hiện. Khách hàng không bị thu phí; chuyến đi chưa đủ điều kiện hỗ trợ tài xế.";
        return new CustomerNoShowReportResponse(
            behaviorEvent.Id, trip.Id, trip.BookingId, 0m, supportEligible,
            support?.SupportAmount ?? 0m, support?.Status, message);
    }
}
