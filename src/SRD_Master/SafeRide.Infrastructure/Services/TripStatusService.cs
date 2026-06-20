using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Bookings;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using System.Text.Json;

namespace SafeRide.Infrastructure.Services;

public sealed class TripStatusService : ITripStatusService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IRedisService _redisService;
    private readonly IRealtimeNotificationService _realtimeNotificationService;

    public TripStatusService(
        ApplicationDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IRedisService redisService,
        IRealtimeNotificationService realtimeNotificationService)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _redisService = redisService;
        _realtimeNotificationService = realtimeNotificationService;
    }

    public async Task UpdateDriverTripStatusAsync(
        Guid driverId,
        long tripId,
        TripStatus tripStatus,
        CancellationToken cancellationToken)
    {
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
        trip.TripStatus = tripStatus;
        switch (tripStatus)
        {
            case TripStatus.ARRIVED:
                trip.ArrivedAt ??= utcNow;
                break;
            case TripStatus.IN_PROGRESS:
                trip.StartedAt ??= utcNow;
                break;
            case TripStatus.COMPLETED:
                trip.CompletedAt ??= utcNow;
                trip.Booking.BookingStatus = BookingStatus.Completed;
                trip.Booking.UpdatedAt = utcNow;
                if (previousTripStatus != TripStatus.COMPLETED &&
                    previousBookingStatus != BookingStatus.Completed)
                {
                    IncrementPromotionUsage(trip.Booking);
                }
                await ReleaseDriverAsync(driverId, utcNow, cancellationToken);
                break;
            case TripStatus.CANCELLED:
                trip.CancelledByUserId = driverId;
                trip.Booking.BookingStatus = BookingStatus.Cancelled;
                trip.Booking.UpdatedAt = utcNow;
                if (previousTripStatus != TripStatus.COMPLETED &&
                    previousBookingStatus != BookingStatus.Completed)
                {
                    RemoveBookingPromotions(trip.Booking);
                }
                await ReleaseDriverAsync(driverId, utcNow, cancellationToken);
                break;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await CacheTripLiveAsync(trip, utcNow);

        await _realtimeNotificationService.PublishTripStatusChangedAsync(
            new TripStatusChangedEvent(
                trip.Id,
                trip.BookingId,
                trip.Booking.CustomerId,
                trip.DriverId,
                trip.TripStatus,
                utcNow),
            cancellationToken);

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
        var cache = new TripLiveCache(
            trip.Id,
            trip.BookingId,
            trip.DriverId,
            trip.Booking.CustomerId,
            trip.TripStatus,
            trip.DriverAssignedAt ?? utcNow);

        await _redisService.SetAsync(
            RedisKeys.TripLive(trip.Id),
            JsonSerializer.Serialize(cache),
            TimeSpan.FromHours(12));
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
        }

        await _redisService.SetAsync(
            RedisKeys.DriverOnline(driverId),
            "1",
            TimeSpan.FromMinutes(5));
        await _redisService.SetAsync(
            RedisKeys.DriverStatus(driverId),
            DriverWorkStatus.Online.ToString(),
            TimeSpan.FromMinutes(5));
    }

    private static void IncrementPromotionUsage(Domain.Entities.Booking booking)
    {
        foreach (var bookingPromotion in booking.BookingPromotions)
        {
            bookingPromotion.Promotion.CurrentUsageCount += 1;
        }
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
            TripStatus.IN_PROGRESS => requested is TripStatus.COMPLETED
                or TripStatus.CANCELLED,
            _ => false
        };
    }
}
