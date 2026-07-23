using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NetTopologySuite.Geometries;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.Trips.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using SafeRide.Infrastructure.Services;

namespace SafeRide.IntegrationTests;

public sealed class TripChatServiceTests
{
    private static readonly DateTime UtcNow =
        new(2026, 7, 19, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SendMessageAsync_WithValidCustomerMessage_TrimsAndStoresMessageInRedis()
    {
        await using var dbContext = CreateDbContext();
        var redis = new InMemoryRedisService();
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var trip = SeedTrip(dbContext, customerId, driverId, TripStatus.IN_PROGRESS);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, redis);

        var result = await service.SendMessageAsync(
            customerId,
            trip.Id,
            "  Anh tới chưa?  ");

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(trip.Id, result.TripId);
        Assert.Equal(customerId, result.SenderUserId);
        Assert.Equal("Text", result.MessageType);
        Assert.Equal("Anh tới chưa?", result.Message);
        Assert.Null(result.ImageUrl);
        Assert.Equal(UtcNow, result.SentAt);

        var stored = await redis.ListRangeAsync(RedisKeys.TripChatMessages(trip.Id));
        var raw = Assert.Single(stored);
        var payload = JsonSerializer.Deserialize<TripChatMessageDto>(
            raw,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(payload);
        Assert.Equal(result.Id, payload.Id);
        Assert.Equal("Text", payload.MessageType);
        Assert.Equal("Anh tới chưa?", payload.Message);
        Assert.Null(payload.ImageUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendMessageAsync_WithEmptyMessage_Throws(string message)
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new InMemoryRedisService());

        var exception = await Assert.ThrowsAsync<BookingException>(
            () => service.SendMessageAsync(Guid.NewGuid(), 1, message));

        Assert.Equal("Nội dung tin nhắn không được để trống.", exception.Message);
    }

    [Fact]
    public async Task SendMessageAsync_WithTooLongMessage_Throws()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new InMemoryRedisService());

        var exception = await Assert.ThrowsAsync<BookingException>(
            () => service.SendMessageAsync(Guid.NewGuid(), 1, new string('a', 1001)));

        Assert.Equal("Nội dung tin nhắn quá dài.", exception.Message);
    }

    [Fact]
    public async Task SendMessageAsync_WithUnauthorizedUser_Throws()
    {
        await using var dbContext = CreateDbContext();
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var otherUserId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var trip = SeedTrip(dbContext, customerId, driverId, TripStatus.IN_PROGRESS);
        SeedUser(dbContext, otherUserId, "Người ngoài");
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, new InMemoryRedisService());

        var exception = await Assert.ThrowsAsync<BookingException>(
            () => service.SendMessageAsync(otherUserId, trip.Id, "Xin chào"));

        Assert.Equal("Bạn không có quyền truy cập cuộc trò chuyện này.", exception.Message);
    }

    [Fact]
    public async Task SendMessageAsync_WithCompletedTrip_StoresMessageInRedis()
    {
        await using var dbContext = CreateDbContext();
        var redis = new InMemoryRedisService();
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var trip = SeedTrip(dbContext, customerId, driverId, TripStatus.COMPLETED);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, redis);

        var result = await service.SendMessageAsync(
            customerId,
            trip.Id,
            "  Cảm ơn anh  ");

        Assert.Equal("Text", result.MessageType);
        Assert.Equal("Cảm ơn anh", result.Message);
        Assert.Single(await redis.ListRangeAsync(RedisKeys.TripChatMessages(trip.Id)));
    }

    [Fact]
    public async Task SendMessageAsync_WithCancelledTrip_Throws()
    {
        await using var dbContext = CreateDbContext();
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var trip = SeedTrip(dbContext, customerId, driverId, TripStatus.CANCELLED);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, new InMemoryRedisService());

        var exception = await Assert.ThrowsAsync<BookingException>(
            () => service.SendMessageAsync(customerId, trip.Id, "Xin chào"));

        Assert.Equal("Không thể gửi tin nhắn cho chuyến đi đã kết thúc.", exception.Message);
    }

    [Fact]
    public async Task GetMessagesAsync_ReturnsMessagesOrderedBySentAt()
    {
        await using var dbContext = CreateDbContext();
        var redis = new InMemoryRedisService();
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var trip = SeedTrip(dbContext, customerId, driverId, TripStatus.IN_PROGRESS);
        await dbContext.SaveChangesAsync();
        await PushMessageAsync(redis, trip.Id, driverId, "Tin 2", UtcNow.AddMinutes(2));
        await PushMessageAsync(redis, trip.Id, customerId, "Tin 1", UtcNow.AddMinutes(1));
        await PushMessageAsync(redis, trip.Id, driverId, "Tin 3", UtcNow.AddMinutes(3));
        var service = CreateService(dbContext, redis);

        var messages = await service.GetMessagesAsync(customerId, trip.Id);

        Assert.Collection(
            messages,
            message => Assert.Equal("Tin 1", message.Message),
            message => Assert.Equal("Tin 2", message.Message),
            message => Assert.Equal("Tin 3", message.Message));
    }

    [Fact]
    public async Task SendMessageAsync_KeepsOnlyLast100MessagesInRedis()
    {
        await using var dbContext = CreateDbContext();
        var redis = new InMemoryRedisService();
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var trip = SeedTrip(dbContext, customerId, driverId, TripStatus.IN_PROGRESS);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, redis);

        for (var index = 1; index <= 105; index++)
        {
            await service.SendMessageAsync(customerId, trip.Id, $"Tin {index}");
        }

        var stored = await redis.ListRangeAsync(RedisKeys.TripChatMessages(trip.Id));
        Assert.Equal(100, stored.Count);
        var first = JsonSerializer.Deserialize<TripChatMessageDto>(
            stored[0],
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var last = JsonSerializer.Deserialize<TripChatMessageDto>(
            stored[^1],
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("Tin 6", first?.Message);
        Assert.Equal("Tin 105", last?.Message);
    }

    [Fact]
    public async Task ShortenMessageTtlAsync_UsesSevenDayTtlForClosedTripMessages()
    {
        await using var dbContext = CreateDbContext();
        var redis = new RedisServiceFake();
        var service = new TripChatService(
            dbContext,
            redis,
            new DateTimeProviderFake(UtcNow),
            new HostEnvironmentFake());

        await service.ShortenMessageTtlAsync(4);

        Assert.Equal(RedisKeys.TripChatMessages(4), redis.ExpiredKey);
        Assert.Equal(TimeSpan.FromDays(7), redis.Expiration);
    }

    [Fact]
    public async Task SendImageMessageAsync_WithValidImage_StoresImageMessageInRedis()
    {
        await using var dbContext = CreateDbContext();
        var redis = new InMemoryRedisService();
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var trip = SeedTrip(dbContext, customerId, driverId, TripStatus.IN_PROGRESS);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, redis);
        await using var image = new MemoryStream([1, 2, 3, 4]);

        var result = await service.SendImageMessageAsync(
            customerId,
            trip.Id,
            image,
            "image/webp",
            image.Length);

        Assert.Equal("Image", result.MessageType);
        Assert.Equal(string.Empty, result.Message);
        Assert.NotNull(result.ImageUrl);
        Assert.StartsWith($"/uploads/trip-chat/{trip.Id}/", result.ImageUrl);
        Assert.EndsWith(".webp", result.ImageUrl);

        var stored = await redis.ListRangeAsync(RedisKeys.TripChatMessages(trip.Id));
        var payload = JsonSerializer.Deserialize<TripChatMessageDto>(
            Assert.Single(stored),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(payload);
        Assert.Equal("Image", payload.MessageType);
        Assert.Equal(result.ImageUrl, payload.ImageUrl);
    }

    [Fact]
    public async Task SendImageMessageAsync_WithUnsupportedContentType_Throws()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateService(dbContext, new InMemoryRedisService());
        await using var image = new MemoryStream([1, 2, 3]);

        var exception = await Assert.ThrowsAsync<BookingException>(
            () => service.SendImageMessageAsync(
                Guid.NewGuid(),
                1,
                image,
                "image/gif",
                image.Length));

        Assert.Equal("Định dạng ảnh không được hỗ trợ.", exception.Message);
    }

    [Fact]
    public async Task SendImageMessageAsync_WithCompletedTrip_StoresImageMessageInRedis()
    {
        await using var dbContext = CreateDbContext();
        var redis = new InMemoryRedisService();
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var trip = SeedTrip(dbContext, customerId, driverId, TripStatus.COMPLETED);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, redis);
        await using var image = new MemoryStream([1, 2, 3]);

        var result = await service.SendImageMessageAsync(
            customerId,
            trip.Id,
            image,
            "image/png",
            image.Length);

        Assert.Equal("Image", result.MessageType);
        Assert.NotNull(result.ImageUrl);
        Assert.Single(await redis.ListRangeAsync(RedisKeys.TripChatMessages(trip.Id)));
    }

    [Fact]
    public async Task SendImageMessageAsync_WithCancelledTrip_Throws()
    {
        await using var dbContext = CreateDbContext();
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var trip = SeedTrip(dbContext, customerId, driverId, TripStatus.CANCELLED);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, new InMemoryRedisService());
        await using var image = new MemoryStream([1, 2, 3]);

        var exception = await Assert.ThrowsAsync<BookingException>(
            () => service.SendImageMessageAsync(
                customerId,
                trip.Id,
                image,
                "image/png",
                image.Length));

        Assert.Equal("Không thể gửi ảnh cho chuyến đi đã kết thúc.", exception.Message);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"trip-chat-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static TripChatService CreateService(
        ApplicationDbContext dbContext,
        InMemoryRedisService redis)
    {
        return new TripChatService(
            dbContext,
            redis,
            new DateTimeProviderFake(UtcNow),
            new HostEnvironmentFake());
    }

    private static Trip SeedTrip(
        ApplicationDbContext dbContext,
        Guid customerId,
        Guid driverId,
        TripStatus tripStatus)
    {
        SeedUser(dbContext, customerId, "Khách hàng");
        SeedUser(dbContext, driverId, "Tài xế");

        var booking = new Booking
        {
            BookingId = 7,
            CustomerId = customerId,
            VehicleId = 1,
            ServiceTypeId = 1,
            BookingStatus = BookingStatus.DriverAssigned,
            PickupAddress = "Điểm đón",
            PickupLocation = new Point(106.660172, 10.762622) { SRID = 4326 },
            DestinationAddress = "Điểm đến",
            DestinationLocation = new Point(106.700000, 10.800000) { SRID = 4326 },
            EstimatedFare = 100000,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow
        };
        var trip = new Trip
        {
            Id = 4,
            BookingId = booking.BookingId,
            Booking = booking,
            DriverId = driverId,
            TripStatus = tripStatus,
            CreatedAt = UtcNow
        };
        booking.Trip = trip;
        dbContext.Bookings.Add(booking);
        dbContext.Trips.Add(trip);
        return trip;
    }

    private static void SeedUser(
        ApplicationDbContext dbContext,
        Guid userId,
        string fullName)
    {
        dbContext.AspNetUsers.Add(new AspNetUser
        {
            Id = userId,
            UserName = userId.ToString(),
            FullName = fullName,
            IsActive = true
        });
    }

    private static Task PushMessageAsync(
        InMemoryRedisService redis,
        long tripId,
        Guid senderUserId,
        string message,
        DateTime sentAt)
    {
        var payload = new TripChatMessageDto(
            Guid.NewGuid(),
            tripId,
            senderUserId,
            "Người gửi",
            "Text",
            message,
            null,
            sentAt);

        return redis.ListRightPushTrimAndExpireAsync(
            RedisKeys.TripChatMessages(tripId),
            JsonSerializer.Serialize(
                payload,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            100,
            TimeSpan.FromHours(24));
    }

    private sealed class DateTimeProviderFake(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class HostEnvironmentFake : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "SafeRide.Tests";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }

    private sealed class RedisServiceFake : IRedisService
    {
        public string? ExpiredKey { get; private set; }

        public TimeSpan? Expiration { get; private set; }

        public Task SetAsync(string key, string value, TimeSpan expiration) =>
            Task.CompletedTask;

        public Task<bool> SetIfNotExistsAsync(
            string key,
            string value,
            TimeSpan expiration) =>
            Task.FromResult(true);

        public Task<bool> TryAcquireDistributedLockAsync(
            string key,
            string value,
            TimeSpan expiration) =>
            Task.FromResult(true);

        public Task<string?> GetAsync(string key) =>
            Task.FromResult<string?>(null);

        public Task<IReadOnlyDictionary<string, string?>> GetManyAsync(
            IReadOnlyCollection<string> keys) =>
            Task.FromResult<IReadOnlyDictionary<string, string?>>(
                keys.ToDictionary(key => key, _ => (string?)null));

        public Task RemoveAsync(string key) =>
            Task.CompletedTask;

        public Task ExpireAsync(
            string key,
            TimeSpan expiration,
            CancellationToken cancellationToken = default)
        {
            ExpiredKey = key;
            Expiration = expiration;
            return Task.CompletedTask;
        }

        public Task ListRightPushTrimAndExpireAsync(
            string key,
            string value,
            int maxLength,
            TimeSpan expiration,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<string>> ListRangeAsync(
            string key,
            long start = 0,
            long stop = -1,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<long> IncrementAsync(string key, TimeSpan expiration) =>
            Task.FromResult(1L);

        public Task GeoAddAsync(
            string key,
            double longitude,
            double latitude,
            string member) =>
            Task.CompletedTask;

        public Task GeoRemoveAsync(
            string key,
            string member,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<string>> GeoRadiusAsync(
            string key,
            double longitude,
            double latitude,
            double radiusKm,
            int count) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<OtpVerificationResult> VerifyAndConsumeOtpAsync(
            string otpKey,
            string attemptsKey,
            string expectedHash,
            int maxAttempts) =>
            Task.FromResult(OtpVerificationResult.Missing);

        public Task<TripTrackingUpdateResult> RecordTripTrackingPointAsync(
            TripTrackingPoint point,
            TripTrackingWriteOptions options,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TripTrackingUpdateResult(false, false, 0, 0, "not_supported"));

        public Task<TripTrackingSnapshot> GetTripTrackingSnapshotAsync(
            long tripId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TripTrackingSnapshot([], 0, null, null, null, null));

        public Task RemoveTripTrackingAsync(
            long tripId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
