using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire delayed job: fired ~BookingExpireAfterMinutes after a booking starts Searching.
/// Expires the booking, cancels/expires any open driver offers, and notifies the customer.
/// Idempotent — safe to call even if the booking is no longer in Searching state.
/// </summary>
public sealed class ExpireSearchingBookingsJob
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRedisService _redisService;
    private readonly IRealtimeNotificationService _realtimeService;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ExpireSearchingBookingsJob> _logger;

    public ExpireSearchingBookingsJob(
        ApplicationDbContext dbContext,
        IRedisService redisService,
        IRealtimeNotificationService realtimeService,
        IDateTimeProvider clock,
        ILogger<ExpireSearchingBookingsJob> logger)
    {
        _dbContext = dbContext;
        _redisService = redisService;
        _realtimeService = realtimeService;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Entry point called by Hangfire.</summary>
    public async Task ExecuteAsync(long bookingId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "ExpireSearchingBookingsJob started for BookingId={BookingId}.", bookingId);

        var booking = await _dbContext.Bookings
            .Include(x => x.DriverOffers)
            .FirstOrDefaultAsync(x => x.BookingId == bookingId, cancellationToken);

        if (booking is null)
        {
            _logger.LogWarning(
                "ExpireSearchingBookingsJob: BookingId={BookingId} not found. Skipping.", bookingId);
            return;
        }

        // Idempotent guard — only expire if still searching.
        if (booking.BookingStatus != BookingStatus.Searching)
        {
            _logger.LogInformation(
                "ExpireSearchingBookingsJob: BookingId={BookingId} is {Status}. No action needed.",
                bookingId, booking.BookingStatus);
            return;
        }

        var utcNow = _clock.UtcNow;
        const string expireMessage =
            "Rất tiếc, SafeRide chưa tìm thấy tài xế phù hợp trong thời gian quy định. " +
            "Booking đã hết hạn, bạn có thể thử đặt lại.";

        booking.BookingStatus = BookingStatus.Expired;
        booking.UpdatedAt = utcNow;

        _dbContext.Notifications.Add(new Notification
        {
            UserId = booking.CustomerId,
            Title = "Booking đã hết hạn",
            Content = expireMessage,
            NotificationType = "BookingExpired",
            SentAt = utcNow
        });

        // Collect events to publish after SaveChanges.
        var offerExpiredEvents = new List<DriverOfferExpiredEvent>();
        var offerCancelledEvents = new List<DriverOfferCancelledEvent>();

        foreach (var offer in booking.DriverOffers.Where(x => IsOpenOfferStatus(x.OfferStatus)))
        {
            if (offer.ExpiresAt <= utcNow)
            {
                offer.OfferStatus = DriverOfferStatus.Expired;
                offer.ExpiredAt = utcNow;
                offerExpiredEvents.Add(new DriverOfferExpiredEvent(
                    offer.BookingId,
                    booking.CustomerId,
                    offer.DriverId,
                    offer.Id,
                    utcNow,
                    "Yêu cầu nhận chuyến đã hết hạn."));
            }
            else
            {
                offer.OfferStatus = DriverOfferStatus.Cancelled;
                offer.CancelledAt = utcNow;
                offerCancelledEvents.Add(new DriverOfferCancelledEvent(
                    offer.BookingId,
                    booking.CustomerId,
                    offer.DriverId,
                    offer.Id,
                    utcNow,
                    "Yêu cầu nhận chuyến đã được hủy vì booking đã hết hạn."));
            }

            await _redisService.RemoveAsync(RedisKeys.MatchingOffer(offer.BookingId, offer.DriverId));
            await _redisService.RemoveAsync(RedisKeys.MatchingDriverLock(offer.DriverId));
        }

        await _redisService.RemoveAsync(RedisKeys.MatchingBooking(bookingId));
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Publish realtime events.
        foreach (var ev in offerExpiredEvents)
        {
            await _realtimeService.PublishDriverOfferExpiredAsync(ev, cancellationToken);
        }

        foreach (var ev in offerCancelledEvents)
        {
            await _realtimeService.PublishDriverOfferCancelledAsync(ev, cancellationToken);
        }

        await _realtimeService.PublishBookingExpiredAsync(
            new BookingExpiredEvent(bookingId, booking.CustomerId, utcNow, expireMessage),
            cancellationToken);

        await _realtimeService.PublishBookingStatusChangedAsync(
            new BookingStatusChangedEvent(
                bookingId,
                booking.CustomerId,
                BookingStatus.Expired,
                utcNow),
            cancellationToken);

        _logger.LogInformation(
            "ExpireSearchingBookingsJob: BookingId={BookingId} expired successfully.", bookingId);
    }

    private static bool IsOpenOfferStatus(DriverOfferStatus status) =>
        status is DriverOfferStatus.Sent or DriverOfferStatus.DriverAccepted;
}
