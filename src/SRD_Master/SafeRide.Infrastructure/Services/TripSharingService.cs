using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Auth;
using SafeRide.Application.Features.TripSharing;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.Infrastructure.Services;

public sealed class TripSharingService : ITripSharingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IRedisService _redisService;
    private readonly IRealtimeNotificationService _realtime;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IOptionsMonitor<TripSharingOptions> _options;
    private readonly ITripShareExpiryScheduler _expiryScheduler;

    public TripSharingService(
        ApplicationDbContext dbContext,
        IRedisService redisService,
        IRealtimeNotificationService realtime,
        IDateTimeProvider dateTimeProvider,
        IOptionsMonitor<TripSharingOptions> options,
        ITripShareExpiryScheduler expiryScheduler)
    {
        _dbContext = dbContext;
        _redisService = redisService;
        _realtime = realtime;
        _dateTimeProvider = dateTimeProvider;
        _options = options;
        _expiryScheduler = expiryScheduler;
    }

    public async Task<CreateTripShareResult> CreateAsync(
        long tripId,
        Guid sharedByUserId,
        string recipientPhoneNumber,
        CancellationToken cancellationToken = default)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        var normalizedPhone = PhoneNumberNormalizer.Normalize(recipientPhoneNumber);
        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            throw Error("trip_share.invalid_phone", "Số điện thoại người nhận không hợp lệ.", StatusCodes.Status400BadRequest);
        }

        var trip = await _dbContext.Trips
            .Include(x => x.Booking)
            .FirstOrDefaultAsync(x => x.Id == tripId, cancellationToken);
        if (trip is null)
        {
            throw Error("trip_share.trip_not_found", "Không tìm thấy chuyến đi.", StatusCodes.Status404NotFound);
        }

        if (trip.Booking.CustomerId != sharedByUserId)
        {
            throw Error("trip_share.owner_required", "Bạn không có quyền chia sẻ chuyến đi này.", StatusCodes.Status403Forbidden);
        }

        if (!IsActiveTrip(trip.TripStatus))
        {
            throw Error("trip_share.trip_not_active", "Chỉ có thể chia sẻ chuyến đi đang hoạt động.", StatusCodes.Status409Conflict);
        }

        var recipient = await _dbContext.Users.FirstOrDefaultAsync(
            x => x.PhoneNumber == normalizedPhone,
            cancellationToken);
        if (recipient is null)
        {
            throw Error("trip_share.recipient_not_found", "Không tìm thấy tài khoản SafeRide với số điện thoại này.", StatusCodes.Status404NotFound);
        }

        if (recipient.Id == sharedByUserId)
        {
            throw Error("trip_share.self_share", "Bạn không thể chia sẻ chuyến đi cho chính mình.", StatusCodes.Status400BadRequest);
        }

        if (!IsUserActive(recipient, utcNow))
        {
            throw Error("trip_share.recipient_inactive", "Tài khoản người nhận hiện không hoạt động.", StatusCodes.Status409Conflict);
        }

        var rawToken = TripShareTokenService.GenerateToken();
        var tokenHash = TripShareTokenService.HashToken(rawToken);
        var expiresAt = utcNow.AddHours(_options.CurrentValue.DefaultExpirationHours);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var share = await _dbContext.TripShares.FirstOrDefaultAsync(
            x => x.TripId == tripId
                && x.RecipientUserId == recipient.Id
                && x.RevokedAt == null,
            cancellationToken);

        if (share is null || share.ExpiresAt <= utcNow)
        {
            if (share is not null)
            {
                // Preserve the expired record for audit history before creating a new share.
                share.RevokedAt = utcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            share = new TripShare
            {
                TripId = tripId,
                SharedByUserId = sharedByUserId,
                RecipientUserId = recipient.Id,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt,
                CreatedAt = utcNow
            };
            _dbContext.TripShares.Add(share);
        }
        else
        {
            // Rotate the token so an active share can be returned without storing plaintext.
            share.SharedByUserId = sharedByUserId;
            share.TokenHash = tokenHash;
            share.ExpiresAt = expiresAt;
            share.OpenedAt = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.Notifications.Add(new Notification
        {
            UserId = recipient.Id,
            Title = "Chuyến đi được chia sẻ",
            Content = $"Một chuyến đi đã được chia sẻ với bạn (mã {share.Id}).",
            NotificationType = "TripShared",
            SentAt = utcNow
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _expiryScheduler.ScheduleExpiration(share.Id, share.ExpiresAt);

        return new CreateTripShareResult(
            share.Id,
            ToRecipient(recipient),
            BuildShareUrl(_options.CurrentValue.AppLinkBaseUrl, rawToken),
            expiresAt);
    }

    public async Task<IReadOnlyList<TripShareListItemDto>> ListAsync(
        long tripId,
        Guid sharedByUserId,
        CancellationToken cancellationToken = default)
    {
        var ownerId = await _dbContext.Trips
            .Where(x => x.Id == tripId)
            .Select(x => (Guid?)x.Booking.CustomerId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!ownerId.HasValue)
        {
            throw Error("trip_share.trip_not_found", "Không tìm thấy chuyến đi.", StatusCodes.Status404NotFound);
        }

        if (ownerId.Value != sharedByUserId)
        {
            throw Error("trip_share.owner_required", "Bạn không có quyền xem danh sách chia sẻ này.", StatusCodes.Status403Forbidden);
        }

        var utcNow = _dateTimeProvider.UtcNow;
        return await _dbContext.TripShares
            .AsNoTracking()
            .Where(x => x.TripId == tripId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new TripShareListItemDto(
                x.Id,
                new TripShareRecipientDto(
                    x.RecipientUserId,
                    x.RecipientUser.FullName ?? "Người dùng SafeRide",
                    x.RecipientUser.AvatarUrl,
                    MaskPhone(x.RecipientUser.PhoneNumber)),
                x.OpenedAt,
                x.ExpiresAt,
                x.RevokedAt,
                x.RevokedAt == null && x.ExpiresAt > utcNow))
            .ToListAsync(cancellationToken);
    }

    public async Task<ResolveTripShareResult> ResolveAsync(
        string rawToken,
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            throw Error("trip_share.invalid_token", "Liên kết chia sẻ không hợp lệ.", StatusCodes.Status404NotFound);
        }

        var tokenHash = TripShareTokenService.HashToken(rawToken);
        var share = await _dbContext.TripShares
            .Include(x => x.Trip)
            .Include(x => x.RecipientUser)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (share is null)
        {
            throw Error("trip_share.invalid_token", "Liên kết chia sẻ không hợp lệ.", StatusCodes.Status404NotFound);
        }

        EnsureRecipient(share, recipientUserId);
        EnsureViewable(share, _dateTimeProvider.UtcNow);
        share.OpenedAt ??= _dateTimeProvider.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ResolveTripShareResult(
            share.Id,
            share.TripId,
            share.Trip.TripStatus.ToString());
    }

    public async Task<SharedTripTrackingDto> GetTrackingAsync(
        long tripShareId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        var share = await _dbContext.TripShares
            .AsSplitQuery()
            .Include(x => x.RecipientUser)
            .Include(x => x.Trip)
                .ThenInclude(x => x.Booking)
                    .ThenInclude(x => x.Vehicle)
            .Include(x => x.Trip)
                .ThenInclude(x => x.Driver)
                    .ThenInclude(x => x.Driver)
            .Include(x => x.Trip)
                .ThenInclude(x => x.Driver)
                    .ThenInclude(x => x.Ratings)
            .FirstOrDefaultAsync(x => x.Id == tripShareId, cancellationToken);
        if (share is null)
        {
            throw Error("trip_share.not_found", "Không tìm thấy lượt chia sẻ.", StatusCodes.Status404NotFound);
        }

        EnsureRecipient(share, recipientUserId);
        EnsureViewable(share, _dateTimeProvider.UtcNow);

        DriverLocationCache? driverLocation = null;
        var locationJson = await _redisService.GetAsync(RedisKeys.DriverLocation(share.Trip.DriverId));
        if (!string.IsNullOrWhiteSpace(locationJson))
        {
            try
            {
                driverLocation = JsonSerializer.Deserialize<DriverLocationCache>(locationJson);
            }
            catch (JsonException)
            {
                // Redis is optional; a malformed cache must not bypass SQL authorization.
            }
        }

        var booking = share.Trip.Booking;
        var vehicle = booking.Vehicle;
        var driver = share.Trip.Driver;
        var averageRating = driver.Ratings.Count == 0
            ? null
            : (double?)driver.Ratings.Average(x => x.RatingScore);

        return new SharedTripTrackingDto(
            share.Id,
            share.Trip.TripStatus.ToString(),
            new SharedTripPointDto(
                booking.PickupLocation.Y,
                booking.PickupLocation.X,
                booking.PickupAddress),
            booking.DestinationLocation is null
                ? null
                : new SharedTripPointDto(
                    booking.DestinationLocation.Y,
                    booking.DestinationLocation.X,
                    booking.DestinationAddress),
            driverLocation is null
                ? null
                : new SharedTripPointDto(driverLocation.Latitude, driverLocation.Longitude),
            driverLocation?.UpdatedAt,
            share.Trip.RoutePolyline ?? booking.RoutePolyline,
            new SharedTripDriverDto(
                driver.Driver.FullName ?? "Tài xế SafeRide",
                driver.Driver.AvatarUrl,
                averageRating),
            new SharedTripVehicleDto(
                vehicle.BrandModel,
                vehicle.Color,
                MaskPlate(vehicle.PlateNumber)),
            null);
    }

    public async Task RevokeAsync(
        long tripId,
        long tripShareId,
        Guid sharedByUserId,
        CancellationToken cancellationToken = default)
    {
        var share = await _dbContext.TripShares
            .Include(x => x.Trip)
                .ThenInclude(x => x.Booking)
            .FirstOrDefaultAsync(
                x => x.Id == tripShareId && x.TripId == tripId,
                cancellationToken);
        if (share is null)
        {
            throw Error("trip_share.not_found", "Không tìm thấy lượt chia sẻ.", StatusCodes.Status404NotFound);
        }

        if (share.Trip.Booking.CustomerId != sharedByUserId)
        {
            throw Error("trip_share.owner_required", "Bạn không có quyền thu hồi lượt chia sẻ này.", StatusCodes.Status403Forbidden);
        }

        if (share.RevokedAt.HasValue)
        {
            return;
        }

        var utcNow = _dateTimeProvider.UtcNow;
        share.RevokedAt = utcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _realtime.PublishSharedTripStatusAsync(
            new SharedTripStatusUpdate(share.Id, share.Trip.TripStatus.ToString(), utcNow),
            "TripShareRevoked",
            cancellationToken);
    }

    public async Task<bool> CanSubscribeAsync(
        long tripShareId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        var utcNow = _dateTimeProvider.UtcNow;
        return await _dbContext.TripShares.AsNoTracking().AnyAsync(
            x => x.Id == tripShareId
                && x.RecipientUserId == recipientUserId
                && x.RecipientUser.IsActive
                && (x.RecipientUser.LockoutEnd == null || x.RecipientUser.LockoutEnd <= utcNow)
                && x.RevokedAt == null
                && x.ExpiresAt > utcNow,
            cancellationToken);
    }

    public async Task PublishLocationAsync(
        long tripId,
        double latitude,
        double longitude,
        DateTime updatedAt,
        CancellationToken cancellationToken = default)
    {
        var shareIds = await _dbContext.TripShares
            .AsNoTracking()
            .Where(x => x.TripId == tripId
                && x.RevokedAt == null
                && x.ExpiresAt > updatedAt
                && x.Trip.TripStatus != TripStatus.COMPLETED
                && x.Trip.TripStatus != TripStatus.CANCELLED)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        await Task.WhenAll(shareIds.Select(id =>
            _realtime.PublishSharedTripLocationUpdatedAsync(
                new SharedTripLocationUpdate(id, latitude, longitude, updatedAt),
                cancellationToken)));
    }

    public async Task HandleTripLifecycleAsync(
        long tripId,
        TripStatus tripStatus,
        DateTime occurredAt,
        CancellationToken cancellationToken = default)
    {
        var shares = await _dbContext.TripShares
            .Where(x => x.TripId == tripId
                && x.RevokedAt == null
                && x.ExpiresAt > occurredAt)
            .ToListAsync(cancellationToken);
        if (shares.Count == 0)
        {
            return;
        }

        var eventName = "SharedTripStatusUpdated";
        if (tripStatus == TripStatus.COMPLETED)
        {
            eventName = "SharedTripCompleted";
            var graceExpiry = occurredAt.AddMinutes(_options.CurrentValue.CompletedGraceMinutes);
            foreach (var share in shares)
            {
                share.ExpiresAt = share.ExpiresAt < graceExpiry ? share.ExpiresAt : graceExpiry;
            }
        }
        else if (tripStatus == TripStatus.CANCELLED)
        {
            eventName = "SharedTripCancelled";
            var graceExpiry = occurredAt.AddMinutes(_options.CurrentValue.CancelledGraceMinutes);
            foreach (var share in shares)
            {
                share.ExpiresAt = share.ExpiresAt < graceExpiry ? share.ExpiresAt : graceExpiry;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        if (tripStatus is TripStatus.COMPLETED or TripStatus.CANCELLED)
        {
            foreach (var share in shares)
            {
                _expiryScheduler.ScheduleExpiration(share.Id, share.ExpiresAt);
            }
        }

        await Task.WhenAll(shares.Select(share =>
            _realtime.PublishSharedTripStatusAsync(
                new SharedTripStatusUpdate(share.Id, tripStatus.ToString(), occurredAt),
                eventName,
                cancellationToken)));
    }

    private void EnsureRecipient(TripShare share, Guid recipientUserId)
    {
        if (share.RecipientUserId != recipientUserId)
        {
            throw Error("trip_share.recipient_required", "Bạn không có quyền mở liên kết chia sẻ này.", StatusCodes.Status403Forbidden);
        }
    }

    private void EnsureViewable(TripShare share, DateTime utcNow)
    {
        if (share.RevokedAt.HasValue || share.ExpiresAt <= utcNow)
        {
            throw Error("trip_share.gone", "Liên kết chia sẻ đã hết hạn hoặc bị thu hồi.", StatusCodes.Status410Gone);
        }

        if (!IsUserActive(share.RecipientUser, utcNow))
        {
            throw Error("trip_share.recipient_inactive", "Tài khoản hiện không hoạt động.", StatusCodes.Status403Forbidden);
        }
    }

    private static bool IsActiveTrip(TripStatus status) =>
        status is not TripStatus.COMPLETED and not TripStatus.CANCELLED;

    private static bool IsUserActive(AspNetUser user, DateTime utcNow) =>
        user.IsActive && (user.LockoutEnd is null || user.LockoutEnd <= utcNow);

    private static TripShareRecipientDto ToRecipient(AspNetUser user) =>
        new(user.Id, user.FullName ?? "Người dùng SafeRide", user.AvatarUrl, MaskPhone(user.PhoneNumber));

    private static string MaskPhone(string? phone)
    {
        var value = phone?.Trim() ?? string.Empty;
        if (value.Length < 7)
        {
            return "***";
        }

        return $"{value[..4]}***{value[^3..]}";
    }

    private static string MaskPlate(string plate)
    {
        var value = plate.Trim();
        return value.Length <= 5 ? "***" : $"{value[..3]}***{value[^2..]}";
    }

    private static string BuildShareUrl(string baseUrl, string rawToken)
    {
        var separator = baseUrl.Contains('?') ? '&' : '?';
        return $"{baseUrl}{separator}t={Uri.EscapeDataString(rawToken)}";
    }

    private static TripSharingException Error(string code, string message, int statusCode) =>
        new(code, message, statusCode);
}
