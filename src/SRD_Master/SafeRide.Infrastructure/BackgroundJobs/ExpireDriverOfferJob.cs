using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.Infrastructure.BackgroundJobs;

public sealed class ExpireDriverOfferJob
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRedisService _redisService;
    private readonly IRealtimeNotificationService _realtimeService;
    private readonly IBookingMatchingService _matchingService;
    private readonly IBookingLifecycleJobScheduler _jobScheduler;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<ExpireDriverOfferJob> _logger;

    public ExpireDriverOfferJob(
        ApplicationDbContext dbContext,
        IRedisService redisService,
        IRealtimeNotificationService realtimeService,
        IBookingMatchingService matchingService,
        IBookingLifecycleJobScheduler jobScheduler,
        IDateTimeProvider clock,
        ILogger<ExpireDriverOfferJob> logger)
    {
        _dbContext = dbContext;
        _redisService = redisService;
        _realtimeService = realtimeService;
        _matchingService = matchingService;
        _jobScheduler = jobScheduler;
        _clock = clock;
        _logger = logger;
    }

    public async Task ExecuteAsync(long offerId, CancellationToken cancellationToken = default)
    {
        var offer = await _dbContext.BookingDriverOffers
            .Include(x => x.Booking)
            .FirstOrDefaultAsync(x => x.Id == offerId, cancellationToken);

        if (offer is null)
        {
            _logger.LogWarning(
                "ExpireDriverOfferJob skipped because OfferId={OfferId} was not found.",
                offerId);
            return;
        }

        if (!IsOpenOfferStatus(offer.OfferStatus))
        {
            await _redisService.RemoveAsync(RedisKeys.HangfireExpireDriverOfferJobId(offerId));
            return;
        }

        var utcNow = _clock.UtcNow;
        if (offer.ExpiresAt > utcNow)
        {
            _jobScheduler.ScheduleExpireDriverOffer(offerId, offer.ExpiresAt - utcNow);
            return;
        }

        var message = offer.OfferStatus == DriverOfferStatus.DriverAccepted
            ? "Tài xế không còn khả dụng. SafeRide đang tìm tài xế khác cho bạn."
            : "Yêu cầu nhận chuyến đã hết hạn.";

        offer.OfferStatus = DriverOfferStatus.Expired;
        offer.ExpiredAt = utcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _redisService.RemoveAsync(RedisKeys.MatchingOffer(offer.BookingId, offer.DriverId));
        await _redisService.RemoveAsync(RedisKeys.MatchingDriverLock(offer.DriverId));
        await _redisService.RemoveAsync(RedisKeys.HangfireExpireDriverOfferJobId(offerId));

        await _realtimeService.PublishDriverOfferExpiredAsync(
            new DriverOfferExpiredEvent(
                offer.BookingId,
                offer.Booking.CustomerId,
                offer.DriverId,
                offer.Id,
                utcNow,
                message),
            cancellationToken);

        if (offer.Booking.BookingStatus == BookingStatus.Searching)
        {
            await _matchingService.StartMatchingAsync(offer.BookingId, cancellationToken);
        }
    }

    private static bool IsOpenOfferStatus(DriverOfferStatus status)
    {
        return status is DriverOfferStatus.Sent
            or DriverOfferStatus.DriverAccepted;
    }
}
