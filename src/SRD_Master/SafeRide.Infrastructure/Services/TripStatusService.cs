using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.Trips.DTOs;
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
    private readonly ITripReturnEvidenceStorage _tripReturnEvidenceStorage;
    private readonly IOptionsMonitor<TripTrackingOptions> _options;

    public TripStatusService(
        ApplicationDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IRedisService redisService,
        IRealtimeNotificationService realtimeNotificationService,
        ITripReturnEvidenceStorage tripReturnEvidenceStorage,
        IOptionsMonitor<TripTrackingOptions> options)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _redisService = redisService;
        _realtimeNotificationService = realtimeNotificationService;
        _tripReturnEvidenceStorage = tripReturnEvidenceStorage;
        _options = options;
    }

    public async Task UpdateDriverTripStatusAsync(
        Guid driverId,
        long tripId,
        TripStatus tripStatus,
        CancellationToken cancellationToken)
    {
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

        await ApplyTripStatusAsync(
            trip,
            tripStatus,
            driverId,
            cancellationToken);
    }

    public async Task CompleteTripAsync(
        Guid userId,
        long tripId,
        CancellationToken cancellationToken)
    {
        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
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

        if (trip.TripStatus != TripStatus.IN_PROGRESS && trip.TripStatus != TripStatus.RETURN_CONFIRMED)
        {
            throw new BookingException(
                "trip.invalid_status_transition",
                "Chỉ có thể hoàn tất chuyến khi chuyến đang di chuyển hoặc đã xác nhận trả xe.",
                409);
        }

        await ApplyTripStatusAsync(
            trip,
            TripStatus.COMPLETED,
            userId,
            cancellationToken);
    }

    public async Task EndTripAsync(
        Guid driverId,
        long tripId,
        CancellationToken cancellationToken)
    {
        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
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

        await ApplyTripStatusAsync(
            trip,
            TripStatus.WAITING_RETURN_CONFIRM,
            driverId,
            cancellationToken);
    }

    public async Task ConfirmReturnByCustomerAsync(
        Guid customerId,
        long tripId,
        bool vehicleReturnedConfirmed,
        CancellationToken cancellationToken)
    {
        if (!vehicleReturnedConfirmed)
        {
            throw new BookingException(
                "trip.return_confirmation_required",
                "Khach hang can xac nhan da nhan lai xe.",
                400);
        }

        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
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

        if (trip.TripStatus != TripStatus.WAITING_RETURN_CONFIRM)
        {
            throw new BookingException(
                "trip.invalid_status_transition",
                "Chi co the xac nhan tra xe khi chuyen dang cho xac nhan.",
                409);
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

        await ApplyTripStatusAsync(
            trip,
            TripStatus.RETURN_CONFIRMED,
            customerId,
            cancellationToken);
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

        if (trip.TripStatus != TripStatus.WAITING_RETURN_CONFIRM)
        {
            throw new BookingException(
                "trip.invalid_status_transition",
                "Chỉ có thể xác nhận trả xe thay khách khi chuyến đang chờ xác nhận.",
                409);
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

        // Upload each evidence photo; order is 1-based to satisfy the DB CHECK (1–3).
        var storedFiles = new List<StoredReturnEvidenceFile>(evidence.Count);
        for (var i = 0; i < evidence.Count; i++)
        {
            var item = evidence[i];
            var stored = await _tripReturnEvidenceStorage.SaveAsync(
                tripId,
                displayOrder: i + 1,
                item.FileName,
                item.ContentType,
                item.Content,
                cancellationToken);
            storedFiles.Add(stored);
        }

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
                if (trip.ActualFare == null)
                {
                    trip.ActualFare = trip.Booking.EstimatedFare;
                    var discountAmount = trip.Booking.BookingPromotions.FirstOrDefault()?.DiscountAmount ?? 0m;
                    trip.FinalFare = Math.Max(0m, trip.Booking.EstimatedFare - discountAmount);
                }
                break;
            case TripStatus.COMPLETED:
                trip.StartedAt ??= utcNow;
                trip.CompletedAt ??= utcNow;
                if (trip.ActualFare == null)
                {
                    trip.ActualFare = trip.Booking.EstimatedFare;
                    var discountAmount = trip.Booking.BookingPromotions.FirstOrDefault()?.DiscountAmount ?? 0m;
                    trip.FinalFare = Math.Max(0m, trip.Booking.EstimatedFare - discountAmount);
                }
                trip.Booking.BookingStatus = BookingStatus.Completed;
                trip.Booking.UpdatedAt = utcNow;
                if (previousTripStatus != TripStatus.COMPLETED &&
                    previousBookingStatus != BookingStatus.Completed)
                {
                    IncrementPromotionUsage(trip.Booking);
                }
                await ReleaseDriverAsync(trip.DriverId, utcNow, cancellationToken);
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
        // Flow: keep live trip cache only for active trips; terminal trips publish final booking status too.
        if (tripStatus is TripStatus.COMPLETED or TripStatus.CANCELLED)
        {
            await _redisService.RemoveAsync(RedisKeys.TripLive(trip.Id));
            await _redisService.RemoveAsync(RedisKeys.DriverActiveTrip(trip.DriverId));
        }
        else
        {
            await CacheTripLiveAsync(trip, utcNow);
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
            assignedAt);

        await _redisService.SetAsync(
            RedisKeys.TripLive(trip.Id),
            JsonSerializer.Serialize(cache),
            TimeSpan.FromHours(_options.CurrentValue.TripLiveTtlHours));
        await _redisService.SetAsync(
            RedisKeys.DriverActiveTrip(trip.DriverId),
            JsonSerializer.Serialize(driverActiveTrip),
            TimeSpan.FromHours(_options.CurrentValue.TripLiveTtlHours));
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
            TripStatus.IN_PROGRESS => requested is TripStatus.COMPLETED || requested is TripStatus.WAITING_RETURN_CONFIRM,
            TripStatus.WAITING_RETURN_CONFIRM => requested is TripStatus.RETURN_CONFIRMED,
            TripStatus.RETURN_CONFIRMED => requested is TripStatus.COMPLETED,
            _ => false
        };
    }
}
