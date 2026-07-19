using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SafeRide.Application.Common.Interfaces;
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
        Assert.Equal("Anh tới chưa?", result.Message);
        Assert.Equal(UtcNow, result.SentAt);

        var stored = await redis.ListRangeAsync(RedisKeys.TripChatMessages(trip.Id));
        var raw = Assert.Single(stored);
        var payload = JsonSerializer.Deserialize<TripChatMessageDto>(
            raw,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(payload);
        Assert.Equal(result.Id, payload.Id);
        Assert.Equal("Anh tới chưa?", payload.Message);
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

    [Theory]
    [InlineData(TripStatus.COMPLETED)]
    [InlineData(TripStatus.CANCELLED)]
    public async Task SendMessageAsync_WithClosedTrip_Throws(TripStatus tripStatus)
    {
        await using var dbContext = CreateDbContext();
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var trip = SeedTrip(dbContext, customerId, driverId, tripStatus);
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
            new DateTimeProviderFake(UtcNow));
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
            message,
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
}
