using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.Trips.DTOs;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.Infrastructure.Services;

public sealed class TripChatService : ITripChatService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan MessageTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan TerminalMessageTtl = TimeSpan.FromHours(2);
    private const int MaxMessages = 100;
    private const int MaxMessageLength = 1000;

    private readonly ApplicationDbContext _dbContext;
    private readonly IRedisService _redisService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public TripChatService(
        ApplicationDbContext dbContext,
        IRedisService redisService,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _redisService = redisService;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task EnsureCanAccessTripChatAsync(
        Guid userId,
        long tripId,
        CancellationToken cancellationToken = default)
    {
        var canAccess = await _dbContext.Trips
            .AsNoTracking()
            .AnyAsync(
                trip => trip.Id == tripId
                    && (trip.DriverId == userId || trip.Booking.CustomerId == userId),
                cancellationToken);
        if (!canAccess)
        {
            throw new BookingException(
                "trip_chat.forbidden",
                "Bạn không có quyền truy cập cuộc trò chuyện này.",
                403);
        }
    }

    public async Task<TripChatMessageDto> SendMessageAsync(
        Guid senderUserId,
        long tripId,
        string message,
        CancellationToken cancellationToken = default)
    {
        var normalizedMessage = NormalizeMessage(message);
        var trip = await _dbContext.Trips
            .AsNoTracking()
            .Include(item => item.Booking)
            .FirstOrDefaultAsync(
                item => item.Id == tripId
                    && (item.DriverId == senderUserId || item.Booking.CustomerId == senderUserId),
                cancellationToken);
        if (trip is null)
        {
            throw new BookingException(
                "trip_chat.forbidden",
                "Bạn không có quyền truy cập cuộc trò chuyện này.",
                403);
        }

        if (trip.TripStatus is TripStatus.COMPLETED or TripStatus.CANCELLED)
        {
            throw new BookingException(
                "trip_chat.trip_closed",
                "Không thể gửi tin nhắn cho chuyến đi đã kết thúc.",
                409);
        }

        var senderName = await ResolveSenderNameAsync(
            senderUserId,
            cancellationToken);
        var payload = new TripChatMessageDto(
            Guid.NewGuid(),
            tripId,
            senderUserId,
            senderName,
            normalizedMessage,
            _dateTimeProvider.UtcNow);

        await _redisService.ListRightPushTrimAndExpireAsync(
            RedisKeys.TripChatMessages(tripId),
            JsonSerializer.Serialize(payload, JsonOptions),
            MaxMessages,
            MessageTtl,
            cancellationToken);

        return payload;
    }

    public async Task<IReadOnlyList<TripChatMessageDto>> GetMessagesAsync(
        Guid userId,
        long tripId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanAccessTripChatAsync(userId, tripId, cancellationToken);

        var values = await _redisService.ListRangeAsync(
            RedisKeys.TripChatMessages(tripId),
            cancellationToken: cancellationToken);

        return values
            .Select(value => JsonSerializer.Deserialize<TripChatMessageDto>(
                value,
                JsonOptions))
            .Where(message => message is not null)
            .Select(message => message!)
            .OrderBy(message => message.SentAt)
            .ToList();
    }

    public Task ShortenMessageTtlAsync(
        long tripId,
        CancellationToken cancellationToken = default)
    {
        return _redisService.ExpireAsync(
            RedisKeys.TripChatMessages(tripId),
            TerminalMessageTtl,
            cancellationToken);
    }

    private static string NormalizeMessage(string message)
    {
        var normalized = message?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new BookingException(
                "trip_chat.message_required",
                "Nội dung tin nhắn không được để trống.",
                400);
        }

        if (normalized.Length > MaxMessageLength)
        {
            throw new BookingException(
                "trip_chat.message_too_long",
                "Nội dung tin nhắn quá dài.",
                400);
        }

        return normalized;
    }

    private async Task<string> ResolveSenderNameAsync(
        Guid senderUserId,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.AspNetUsers
            .AsNoTracking()
            .Where(item => item.Id == senderUserId)
            .Select(item => new
            {
                item.FullName,
                item.UserName,
                item.Email
            })
            .FirstOrDefaultAsync(cancellationToken);

        return user?.FullName?.Trim()
            ?? user?.UserName?.Trim()
            ?? user?.Email?.Trim()
            ?? senderUserId.ToString();
    }
}
