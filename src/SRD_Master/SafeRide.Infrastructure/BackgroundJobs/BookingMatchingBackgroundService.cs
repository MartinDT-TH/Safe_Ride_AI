using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.Infrastructure.BackgroundJobs;

public sealed class BookingMatchingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingMatchingBackgroundService> _logger;

    public BookingMatchingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingMatchingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessMatchingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Booking matching background cycle failed.");
            }

            var delay = await GetDelayAsync(stoppingToken);
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task<TimeSpan> GetDelayAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var policyProvider = scope.ServiceProvider.GetRequiredService<IMatchingPolicyProvider>();
        return TimeSpan.FromSeconds(Math.Max(1, policyProvider.Current.MatchingTickSeconds));
    }

    private async Task ProcessMatchingAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        var policyProvider = scope.ServiceProvider.GetRequiredService<IMatchingPolicyProvider>();
        var redisService = scope.ServiceProvider.GetRequiredService<IRedisService>();
        var realtimeService = scope.ServiceProvider.GetRequiredService<IRealtimeNotificationService>();
        var matchingService = scope.ServiceProvider.GetRequiredService<IBookingMatchingService>();

        var utcNow = clock.UtcNow;
        var expiredOfferEvents = await ExpireDriverOffersAsync(
            dbContext,
            redisService,
            utcNow,
            cancellationToken);

        var bookingEvents = await ProcessSearchingBookingsAsync(
            dbContext,
            policyProvider,
            redisService,
            utcNow,
            cancellationToken);

        if (expiredOfferEvents.Count > 0 || bookingEvents.HasChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var notification in expiredOfferEvents)
        {
            await realtimeService.PublishDriverOfferExpiredAsync(notification, cancellationToken);
        }

        foreach (var notification in bookingEvents.OfferExpiredEvents)
        {
            await realtimeService.PublishDriverOfferExpiredAsync(notification, cancellationToken);
        }

        foreach (var notification in bookingEvents.OfferCancelledEvents)
        {
            await realtimeService.PublishDriverOfferCancelledAsync(notification, cancellationToken);
        }

        foreach (var notification in bookingEvents.RadiusExpandedEvents)
        {
            await realtimeService.PublishBookingSearchRadiusExpandedAsync(notification, cancellationToken);
        }

        foreach (var notification in bookingEvents.BookingExpiredEvents)
        {
            await realtimeService.PublishBookingExpiredAsync(notification, cancellationToken);
            await realtimeService.PublishBookingStatusChangedAsync(
                new BookingStatusChangedEvent(
                    notification.BookingId,
                    notification.CustomerId,
                    BookingStatus.Expired,
                    notification.ExpiredAt),
                cancellationToken);
        }

        foreach (var bookingId in bookingEvents.BookingsToRetry)
        {
            await matchingService.StartMatchingAsync(bookingId, cancellationToken);
        }
    }

    private static async Task<List<DriverOfferExpiredEvent>> ExpireDriverOffersAsync(
        ApplicationDbContext dbContext,
        IRedisService redisService,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var staleOffers = await dbContext.BookingDriverOffers
            .Include(x => x.Booking)
            .Where(x => (x.OfferStatus == DriverOfferStatus.Sent
                    || x.OfferStatus == DriverOfferStatus.DriverAccepted)
                && x.ExpiresAt <= utcNow)
            .ToListAsync(cancellationToken);

        var events = new List<DriverOfferExpiredEvent>();
        foreach (var offer in staleOffers)
        {
            var message = offer.OfferStatus == DriverOfferStatus.DriverAccepted
                ? "Tài xế không còn khả dụng. SafeRide đang tìm tài xế khác cho bạn."
                : "Yêu cầu nhận chuyến đã hết hạn.";

            offer.OfferStatus = DriverOfferStatus.Expired;
            offer.ExpiredAt = utcNow;
            await redisService.RemoveAsync(RedisKeys.MatchingOffer(offer.BookingId, offer.DriverId));
            await redisService.RemoveAsync(RedisKeys.MatchingDriverLock(offer.DriverId));

            events.Add(new DriverOfferExpiredEvent(
                offer.BookingId,
                offer.Booking.CustomerId,
                offer.DriverId,
                offer.Id,
                utcNow,
                message));
        }

        return events;
    }

    private static async Task<MatchingCycleEvents> ProcessSearchingBookingsAsync(
        ApplicationDbContext dbContext,
        IMatchingPolicyProvider policyProvider,
        IRedisService redisService,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var bookings = await dbContext.Bookings
            .Include(x => x.DriverOffers)
            .Where(x => x.BookingStatus == BookingStatus.Searching)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var events = new MatchingCycleEvents();
        foreach (var booking in bookings)
        {
            var snapshot = policyProvider.GetSnapshot(booking, utcNow);
            if (snapshot.ExpiresAt.HasValue && utcNow >= snapshot.ExpiresAt.Value)
            {
                booking.BookingStatus = BookingStatus.Expired;
                booking.UpdatedAt = utcNow;
                events.HasChanges = true;

                dbContext.Notifications.Add(new Notification
                {
                    UserId = booking.CustomerId,
                    Title = "Booking đã hết hạn",
                    Content = "Rất tiếc, SafeRide chưa tìm thấy tài xế phù hợp trong thời gian quy định. Booking đã hết hạn, bạn có thể thử đặt lại.",
                    NotificationType = "BookingExpired",
                    SentAt = utcNow
                });

                await redisService.RemoveAsync(RedisKeys.MatchingBooking(booking.BookingId));

                foreach (var offer in booking.DriverOffers.Where(x => IsOpenOfferStatus(x.OfferStatus)))
                {
                    if (offer.ExpiresAt <= utcNow)
                    {
                        offer.OfferStatus = DriverOfferStatus.Expired;
                        offer.ExpiredAt = utcNow;
                        events.OfferExpiredEvents.Add(new DriverOfferExpiredEvent(
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
                        events.OfferCancelledEvents.Add(new DriverOfferCancelledEvent(
                            offer.BookingId,
                            booking.CustomerId,
                            offer.DriverId,
                            offer.Id,
                            utcNow,
                            "Yêu cầu nhận chuyến đã được hủy vì booking đã hết hạn."));
                    }

                    await redisService.RemoveAsync(RedisKeys.MatchingOffer(offer.BookingId, offer.DriverId));
                    await redisService.RemoveAsync(RedisKeys.MatchingDriverLock(offer.DriverId));
                }

                events.BookingExpiredEvents.Add(new BookingExpiredEvent(
                    booking.BookingId,
                    booking.CustomerId,
                    utcNow,
                    "Rất tiếc, SafeRide chưa tìm thấy tài xế phù hợp trong thời gian quy định. Booking đã hết hạn, bạn có thể thử đặt lại."));
                continue;
            }

            if (snapshot.IsExpanded)
            {
                var notified = await redisService.SetIfNotExistsAsync(
                    RedisKeys.BookingRadiusExpandedNotified(booking.BookingId),
                    "1",
                    TimeSpan.FromMinutes(15));
                if (notified)
                {
                    dbContext.Notifications.Add(new Notification
                    {
                        UserId = booking.CustomerId,
                        Title = "Mở rộng phạm vi tìm kiếm",
                        Content = "Chưa tìm thấy tài xế phù hợp trong 5km. SafeRide đang mở rộng phạm vi tìm kiếm lên 10km.",
                        NotificationType = "BookingSearchRadiusExpanded",
                        SentAt = utcNow
                    });
                    events.HasChanges = true;
                    events.RadiusExpandedEvents.Add(new BookingSearchRadiusExpandedEvent(
                        booking.BookingId,
                        booking.CustomerId,
                        policyProvider.Current.InitialRadiusKm,
                        policyProvider.Current.ExpandedRadiusKm,
                        utcNow,
                        "Chưa tìm thấy tài xế phù hợp trong 5km. SafeRide đang mở rộng phạm vi tìm kiếm lên 10km."));
                }
            }

            events.BookingsToRetry.Add(booking.BookingId);
        }

        return events;
    }

    private static bool IsOpenOfferStatus(DriverOfferStatus status)
    {
        return status is DriverOfferStatus.Sent
            or DriverOfferStatus.DriverAccepted;
    }

    private sealed class MatchingCycleEvents
    {
        public bool HasChanges { get; set; }

        public List<DriverOfferExpiredEvent> OfferExpiredEvents { get; } = [];

        public List<DriverOfferCancelledEvent> OfferCancelledEvents { get; } = [];

        public List<BookingSearchRadiusExpandedEvent> RadiusExpandedEvents { get; } = [];

        public List<BookingExpiredEvent> BookingExpiredEvents { get; } = [];

        public List<long> BookingsToRetry { get; } = [];
    }
}
