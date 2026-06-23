using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.Infrastructure.BackgroundJobs;

/// <summary>
/// Hangfire delayed job: fired ~ExpandAfterMinutes after a booking starts Searching.
/// Sends a realtime notification to the customer that the search radius has been expanded.
/// Idempotent — safe to call even if the booking is no longer in Searching state.
/// </summary>
public sealed class ExpandSearchingRadiusJob
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMatchingPolicyProvider _policyProvider;
    private readonly IRedisService _redisService;
    private readonly IRealtimeNotificationService _realtimeService;
    private readonly IDateTimeProvider _clock;
    private readonly IOptionsMonitor<ExpandSearchingRadiusJobOptions> _jobOptions;
    private readonly ILogger<ExpandSearchingRadiusJob> _logger;

    public ExpandSearchingRadiusJob(
        ApplicationDbContext dbContext,
        IMatchingPolicyProvider policyProvider,
        IRedisService redisService,
        IRealtimeNotificationService realtimeService,
        IDateTimeProvider clock,
        IOptionsMonitor<ExpandSearchingRadiusJobOptions> jobOptions,
        ILogger<ExpandSearchingRadiusJob> logger)
    {
        _dbContext = dbContext;
        _policyProvider = policyProvider;
        _redisService = redisService;
        _realtimeService = realtimeService;
        _clock = clock;
        _jobOptions = jobOptions;
        _logger = logger;
    }

    /// <summary>Entry point called by Hangfire.</summary>
    public async Task ExecuteAsync(long bookingId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "ExpandSearchingRadiusJob started for BookingId={BookingId}.", bookingId);

        var booking = await _dbContext.Bookings
            .FirstOrDefaultAsync(x => x.BookingId == bookingId, cancellationToken);

        if (booking is null)
        {
            _logger.LogWarning(
                "ExpandSearchingRadiusJob: BookingId={BookingId} not found. Skipping.", bookingId);
            return;
        }

        // Idempotent guard — only act if still searching.
        if (booking.BookingStatus != BookingStatus.Searching)
        {
            _logger.LogInformation(
                "ExpandSearchingRadiusJob: BookingId={BookingId} is {Status}. No action needed.",
                bookingId, booking.BookingStatus);
            return;
        }

        var utcNow = _clock.UtcNow;

        // Use SetIfNotExists so even if the job fires twice we only notify once.
        var notified = await _redisService.SetIfNotExistsAsync(
            RedisKeys.BookingRadiusExpandedNotified(bookingId),
            "1",
            TimeSpan.FromMinutes(_jobOptions.CurrentValue.RadiusExpandedNotificationTtlMinutes));

        if (!notified)
        {
            _logger.LogInformation(
                "ExpandSearchingRadiusJob: BookingId={BookingId} already notified. Skipping.", bookingId);
            return;
        }

        var options = _policyProvider.Current;

        _dbContext.Notifications.Add(new Notification
        {
            UserId = booking.CustomerId,
            Title = "Mở rộng phạm vi tìm kiếm",
            Content = $"Chưa tìm thấy tài xế phù hợp trong {options.InitialRadiusKm:0.#}km. " +
                      $"SafeRide đang mở rộng phạm vi tìm kiếm lên {options.ExpandedRadiusKm:0.#}km.",
            NotificationType = "BookingSearchRadiusExpanded",
            SentAt = utcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _realtimeService.PublishBookingSearchRadiusExpandedAsync(
            new BookingSearchRadiusExpandedEvent(
                bookingId,
                booking.CustomerId,
                options.InitialRadiusKm,
                options.ExpandedRadiusKm,
                utcNow,
                $"Chưa tìm thấy tài xế phù hợp trong {options.InitialRadiusKm:0.#}km. " +
                $"SafeRide đang mở rộng phạm vi tìm kiếm lên {options.ExpandedRadiusKm:0.#}km."),
            cancellationToken);

        _logger.LogInformation(
            "ExpandSearchingRadiusJob: BookingId={BookingId} radius expanded from {InitKm}km to {ExpKm}km.",
            bookingId, options.InitialRadiusKm, options.ExpandedRadiusKm);
    }
}
