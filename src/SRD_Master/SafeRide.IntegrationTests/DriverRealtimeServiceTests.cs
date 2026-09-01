using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using SafeRide.Application.Common.Exceptions;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Common.Realtime;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using SafeRide.Infrastructure.Services;

namespace SafeRide.IntegrationTests;

[Trait(SqlServerTestDatabase.ProviderTraitName, SqlServerTestDatabase.InMemoryProvider)]
public sealed class DriverRealtimeServiceTests
{
    private static readonly DateTime UtcNow =
        new(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SetDriverOnline_WhenApprovedLicenseIsExpired_RejectsRequest()
    {
        await using var dbContext = CreateDbContext();
        var redis = new InMemoryRedisService();
        var realtime = new RealtimeNotificationServiceFake();
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        SeedDriver(dbContext, driverId);
        dbContext.DriverKycs.Add(NewDriverLicense(
            driverId,
            DateOnly.FromDateTime(UtcNow.AddDays(-1))));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, redis, realtime);

        var exception = await Assert.ThrowsAsync<DriverLicenseExpiredException>(
            () => service.SetDriverOnlineAsync(
                driverId,
                10.762622,
                106.660172,
                CancellationToken.None));

        Assert.Equal(DateOnly.FromDateTime(UtcNow.AddDays(-1)), exception.ExpiryDate);
    }

    [Fact]
    public async Task SetDriverOnline_WhenApprovedLicenseIsValid_SetsOnlineCache()
    {
        await using var dbContext = CreateDbContext();
        var redis = new InMemoryRedisService();
        var realtime = new RealtimeNotificationServiceFake();
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        SeedDriver(dbContext, driverId);
        dbContext.DriverKycs.Add(NewDriverLicense(
            driverId,
            DateOnly.FromDateTime(UtcNow.AddDays(1))));
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext, redis, realtime);

        await service.SetDriverOnlineAsync(
            driverId,
            10.762622,
            106.660172,
            CancellationToken.None);

        Assert.Equal(
            DriverWorkStatus.Online.ToString(),
            await redis.GetAsync(RedisKeys.DriverStatus(driverId)));
    }

    [Fact]
    public async Task UpdateDriverLocation_UsesCachedActiveTrip()
    {
        await using var dbContext = CreateDbContext();
        var redis = new InMemoryRedisService();
        var realtime = new RealtimeNotificationServiceFake();
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await redis.SetAsync(
            RedisKeys.DriverActiveTrip(driverId),
            System.Text.Json.JsonSerializer.Serialize(new DriverActiveTripCache(
                42,
                84,
                driverId,
                customerId,
                TripStatus.IN_PROGRESS,
                UtcNow.AddMinutes(-10),
                "test-route-polyline",
                10.780000,
                106.680000)),
            TimeSpan.FromMinutes(30));
        await redis.SetIfNotExistsAsync(
            RedisKeys.DriverHeartbeatThrottle(driverId),
            "1",
            TimeSpan.FromMinutes(1));
        var service = CreateService(dbContext, redis, realtime);

        await service.UpdateDriverLocationAsync(
            driverId,
            10.762622,
            106.660172,
            CancellationToken.None);

        var notification = Assert.Single(realtime.DriverLocationNotifications);
        Assert.Equal(42, notification.TripId);
        Assert.Equal(customerId, notification.CustomerId);
        Assert.Equal(0, dbContext.SaveChangesCount);
    }

    [Fact]
    public async Task UpdateDriverLocation_MissingActiveTripCache_FallsBackToDbAndRepopulatesCache()
    {
        await using var dbContext = CreateDbContext();
        var redis = new InMemoryRedisService();
        var realtime = new RealtimeNotificationServiceFake();
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var trip = SeedActiveTrip(dbContext, customerId, driverId);
        await dbContext.SaveChangesAsync();
        dbContext.SaveChangesCount = 0;
        await redis.SetIfNotExistsAsync(
            RedisKeys.DriverHeartbeatThrottle(driverId),
            "1",
            TimeSpan.FromMinutes(1));
        var service = CreateService(dbContext, redis, realtime);

        await service.UpdateDriverLocationAsync(
            driverId,
            10.762622,
            106.660172,
            CancellationToken.None);

        var cached = await redis.GetAsync(RedisKeys.DriverActiveTrip(driverId));
        Assert.NotNull(cached);
        var cachedTrip = System.Text.Json.JsonSerializer.Deserialize<DriverActiveTripCache>(cached);
        Assert.NotNull(cachedTrip);
        Assert.Equal(trip.Id, cachedTrip.TripId);
        Assert.Equal(trip.Id, Assert.Single(realtime.DriverLocationNotifications).TripId);
        Assert.Equal(0, dbContext.SaveChangesCount);
    }

    [Fact]
    public async Task UpdateDriverLocation_ThrottlesDbHeartbeatWrites()
    {
        await using var dbContext = CreateDbContext();
        var redis = new InMemoryRedisService();
        var realtime = new RealtimeNotificationServiceFake();
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        SeedDriver(dbContext, driverId);
        await dbContext.SaveChangesAsync();
        dbContext.SaveChangesCount = 0;
        var service = CreateService(dbContext, redis, realtime);

        await service.UpdateDriverLocationAsync(
            driverId,
            10.762622,
            106.660172,
            CancellationToken.None);
        await service.UpdateDriverLocationAsync(
            driverId,
            10.762700,
            106.660200,
            CancellationToken.None);

        Assert.Equal(1, dbContext.SaveChangesCount);
    }

    [Fact]
    public async Task UpdateDriverLocation_WithoutActiveTrip_PublishesDriverOnlyLocation()
    {
        await using var dbContext = CreateDbContext();
        var redis = new InMemoryRedisService();
        var realtime = new RealtimeNotificationServiceFake();
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        await redis.SetIfNotExistsAsync(
            RedisKeys.DriverHeartbeatThrottle(driverId),
            "1",
            TimeSpan.FromMinutes(1));
        var service = CreateService(dbContext, redis, realtime);

        await service.UpdateDriverLocationAsync(
            driverId,
            10.762622,
            106.660172,
            CancellationToken.None);

        var notification = Assert.Single(realtime.DriverLocationNotifications);
        Assert.Null(notification.TripId);
        Assert.Null(notification.CustomerId);
    }

    [Fact]
    public async Task UpdateDriverLocation_WhenTripInProgress_RecordsTripTrackingDistance()
    {
        await using var dbContext = CreateDbContext();
        var redis = new InMemoryRedisService();
        var realtime = new RealtimeNotificationServiceFake();
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await redis.SetAsync(
            RedisKeys.DriverActiveTrip(driverId),
            System.Text.Json.JsonSerializer.Serialize(new DriverActiveTripCache(
                42,
                84,
                driverId,
                customerId,
                TripStatus.IN_PROGRESS,
                UtcNow.AddMinutes(-10),
                "test-route-polyline",
                10.780000,
                106.680000)),
            TimeSpan.FromMinutes(30));
        await redis.SetIfNotExistsAsync(
            RedisKeys.DriverHeartbeatThrottle(driverId),
            "1",
            TimeSpan.FromMinutes(1));
        var service = CreateService(dbContext, redis, realtime);

        await service.UpdateDriverLocationAsync(
            driverId,
            new DriverLocationUpdateInput(
                10.762622,
                106.660172,
                UtcNow,
                1,
                5,
                8),
            CancellationToken.None);
        await service.UpdateDriverLocationAsync(
            driverId,
            new DriverLocationUpdateInput(
                10.763622,
                106.660172,
                UtcNow.AddSeconds(10),
                2,
                5,
                8),
            CancellationToken.None);

        var snapshot = await redis.GetTripTrackingSnapshotAsync(42);
        Assert.True(snapshot.DistanceMeters > 0);
        Assert.NotNull(snapshot.FirstAcceptedPoint);
        Assert.NotNull(snapshot.LastAcceptedPoint);
        Assert.Equal(2, snapshot.PathPoints.Count);
    }

    [Fact]
    public async Task UpdateDriverLocation_WhenTripNotInProgress_DoesNotRecordTripTracking()
    {
        await using var dbContext = CreateDbContext();
        var redis = new InMemoryRedisService();
        var realtime = new RealtimeNotificationServiceFake();
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await redis.SetAsync(
            RedisKeys.DriverActiveTrip(driverId),
            System.Text.Json.JsonSerializer.Serialize(new DriverActiveTripCache(
                42,
                84,
                driverId,
                customerId,
                TripStatus.ACCEPTED,
                UtcNow.AddMinutes(-10))),
            TimeSpan.FromMinutes(30));
        var service = CreateService(dbContext, redis, realtime);

        await service.UpdateDriverLocationAsync(
            driverId,
            new DriverLocationUpdateInput(
                10.762622,
                106.660172,
                UtcNow,
                1,
                5,
                8),
            CancellationToken.None);

        var snapshot = await redis.GetTripTrackingSnapshotAsync(42);
        Assert.Empty(snapshot.PathPoints);
        Assert.Equal(0, snapshot.DistanceMeters);
        Assert.Equal(42, Assert.Single(realtime.DriverLocationNotifications).TripId);
    }

    [Fact]
    public async Task UpdateDriverLocation_PlannedProgressNeverMovesBackward()
    {
        await using var dbContext = CreateDbContext();
        var redis = new InMemoryRedisService();
        var realtime = new RealtimeNotificationServiceFake();
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedCachedTripAsync(redis, driverId, customerId);
        var service = CreateService(dbContext, redis, realtime);

        await service.UpdateDriverLocationAsync(driverId, 43.252, -126.453);
        await service.UpdateDriverLocationAsync(driverId, 38.5, -120.2);

        var progress = await ReadMaximumPlannedProgressAsync(redis, 42);
        Assert.Equal(1d, progress, precision: 6);
    }

    [Fact]
    public async Task UpdateDriverLocation_UntrustedProjectionPreservesPreviousPlannedProgress()
    {
        await using var dbContext = CreateDbContext();
        var redis = new InMemoryRedisService();
        var realtime = new RealtimeNotificationServiceFake();
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedCachedTripAsync(redis, driverId, customerId);
        await redis.SetAsync(
            RedisKeys.TripPlannedRouteProgress(42),
            "0.6",
            TimeSpan.FromHours(1));
        var service = CreateService(dbContext, redis, realtime);

        await service.UpdateDriverLocationAsync(driverId, 0, 0);

        var progress = await ReadMaximumPlannedProgressAsync(redis, 42);
        Assert.Equal(0.6d, progress, precision: 6);
    }

    [Fact]
    public async Task UpdateDriverLocation_ActiveRerouteDoesNotReplaceOriginalPricingRoute()
    {
        await using var dbContext = CreateDbContext();
        var redis = new InMemoryRedisService();
        var realtime = new RealtimeNotificationServiceFake();
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedCachedTripAsync(redis, driverId, customerId);
        await redis.SetAsync(
            RedisKeys.TripActiveRoute(42),
            System.Text.Json.JsonSerializer.Serialize(new
            {
                EncodedPolyline = "_ulLnnqC_mqNvxq`@",
                DistanceMeters = 1d,
                DurationSeconds = 1d,
                UpdatedAt = UtcNow,
                RouteVersion = 2
            }),
            TimeSpan.FromHours(1));
        var service = CreateService(dbContext, redis, realtime);

        await service.UpdateDriverLocationAsync(driverId, 38.5, -120.2);

        var progress = await ReadMaximumPlannedProgressAsync(redis, 42);
        Assert.Equal(0d, progress, precision: 6);
    }

    private static async Task SeedCachedTripAsync(
        IRedisService redis,
        Guid driverId,
        Guid customerId)
    {
        await redis.SetAsync(
            RedisKeys.DriverActiveTrip(driverId),
            System.Text.Json.JsonSerializer.Serialize(new DriverActiveTripCache(
                42,
                84,
                driverId,
                customerId,
                TripStatus.IN_PROGRESS,
                UtcNow.AddMinutes(-10),
                "_p~iF~ps|U_ulLnnqC_mqNvxq`@",
                43.252,
                -126.453)),
            TimeSpan.FromMinutes(30));
    }

    private static async Task<double> ReadMaximumPlannedProgressAsync(
        IRedisService redis,
        long tripId)
    {
        var json = await redis.GetAsync(RedisKeys.TripPlannedRouteProgress(tripId));
        Assert.False(string.IsNullOrWhiteSpace(json));
        return double.Parse(
            json!,
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static DriverRealtimeService CreateService(
        CountingApplicationDbContext dbContext,
        IRedisService redis,
        RealtimeNotificationServiceFake realtime)
    {
        return new DriverRealtimeService(
            dbContext,
            redis,
            new DateTimeProviderFake(UtcNow),
            realtime,
            new TripSharingServiceFake(),
            new OptionsMonitorFake<DriverRealtimeOptions>(
                new DriverRealtimeOptions
                {
                    DriverHeartbeatDbUpdateIntervalSeconds = 60,
                    DriverLocationTtlMinutes = 60,
                    DriverOnlineTtlMinutes = 60
                }),
            new OptionsMonitorFake<TripTrackingOptions>(
                new TripTrackingOptions
                {
                    AccumulatorJitterThresholdMeters = 5,
                    PathSampleDistanceMeters = 0,
                    PathSampleIntervalSeconds = 1,
                    MaxInferredSpeedKmh = 130
                }),
            new MapRoutingServiceFake(),
            NullLogger<DriverRealtimeService>.Instance);
    }

    private static CountingApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"driver-realtime-{Guid.NewGuid():N}")
            .Options;
        return new CountingApplicationDbContext(options);
    }

    private static Trip SeedActiveTrip(
        ApplicationDbContext dbContext,
        Guid customerId,
        Guid driverId)
    {
        var customer = new AspNetUser
        {
            Id = customerId,
            UserName = "customer@example.test",
            Email = "customer@example.test",
            FullName = "Customer",
            IsActive = true,
            CreatedAt = UtcNow
        };
        var driverUser = new AspNetUser
        {
            Id = driverId,
            UserName = "driver@example.test",
            Email = "driver@example.test",
            FullName = "Driver",
            IsActive = true,
            CreatedAt = UtcNow
        };
        var serviceType = new ServiceType
        {
            ServiceName = "Ride"
        };
        var vehicle = new Vehicle
        {
            OwnerUserId = customerId,
            OwnerUser = customer,
            PlateNumber = "29A1-12345",
            BrandModel = "Honda Vision",
            RequiredLicenseClass = RequiredLicenseClass.A1,
            VehicleType = VehicleType.Motorbike,
            EngineType = EngineType.ICE,
            TransmissionType = TransmissionType.None,
            EngineCapacityCc = 110,
            CreatedAt = UtcNow
        };
        var booking = new Booking
        {
            CustomerId = customerId,
            Customer = customer,
            Vehicle = vehicle,
            ServiceType = serviceType,
            BookingType = BookingType.Now,
            BookingStatus = BookingStatus.DriverAssigned,
            PickupAddress = "Pickup",
            PickupLocation = new Point(106.660172, 10.762622) { SRID = 4326 },
            DestinationAddress = "Destination",
            DestinationLocation = new Point(106.651856, 10.818797) { SRID = 4326 },
            EstimatedDistanceKm = 5.2m,
            EstimatedDurationMinutes = 30,
            EstimatedFare = 72_000m,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow
        };
        var trip = new Trip
        {
            Booking = booking,
            DriverId = driverId,
            TripStatus = TripStatus.IN_PROGRESS,
            DriverAssignedAt = UtcNow.AddMinutes(-10),
            StartedAt = UtcNow.AddMinutes(-5),
            CreatedAt = UtcNow.AddMinutes(-10)
        };
        booking.Trip = trip;
        var driver = new DriverProfile
        {
            DriverId = driverId,
            Driver = driverUser,
            IdentityCardNumber = "123456789",
            WorkStatus = DriverWorkStatus.Busy,
            LastActiveAt = UtcNow.AddMinutes(-1),
            CreatedAt = UtcNow.AddDays(-1)
        };

        dbContext.AspNetUsers.AddRange(customer, driverUser);
        dbContext.DriverProfiles.Add(driver);
        dbContext.Bookings.Add(booking);

        return trip;
    }

    private static void SeedDriver(
        ApplicationDbContext dbContext,
        Guid driverId)
    {
        var driverUser = new AspNetUser
        {
            Id = driverId,
            UserName = "driver@example.test",
            Email = "driver@example.test",
            FullName = "Driver",
            IsActive = true,
            CreatedAt = UtcNow
        };
        dbContext.AspNetUsers.Add(driverUser);
        dbContext.DriverProfiles.Add(new DriverProfile
        {
            DriverId = driverId,
            Driver = driverUser,
            IdentityCardNumber = "123456789",
            WorkStatus = DriverWorkStatus.Online,
            LastActiveAt = UtcNow.AddMinutes(-10),
            CreatedAt = UtcNow.AddDays(-1)
        });
    }

    private static DriverKyc NewDriverLicense(Guid driverId, DateOnly expiryDate)
    {
        return new DriverKyc
        {
            DriverId = driverId,
            DocumentType = KycDocumentType.DRIVING_LICENSE,
            DocumentNumber = "B-123456",
            LicenseClass = LicenseClass.B,
            FrontImageUrl = "https://example.test/license.jpg",
            IssueDate = expiryDate.AddYears(-5),
            ExpiryDate = expiryDate,
            KycStatus = KycStatus.Approved,
            CreatedAt = UtcNow.AddYears(-1),
            VerifiedAt = UtcNow.AddYears(-1)
        };
    }

    private sealed class CountingApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : ApplicationDbContext(options, new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider())
    {
        public int SaveChangesCount { get; set; }

        public override Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class DateTimeProviderFake(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class MapRoutingServiceFake : IMapRoutingService
    {
        public Task<RouteEstimateResult> GetRouteEstimateAsync(
            RouteEstimateRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Routing should not be called by these tracking tests.");
    }

    private sealed class RealtimeNotificationServiceFake : IRealtimeNotificationService
    {
        public List<DriverLocationUpdatedEvent> DriverLocationNotifications { get; } = [];

        public Task PublishDriverLocationUpdatedAsync(
            DriverLocationUpdatedEvent notification,
            CancellationToken cancellationToken = default)
        {
            DriverLocationNotifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task PublishBookingStatusChangedAsync(
            BookingStatusChangedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishBookingSearchingStartedAsync(
            BookingSearchingStartedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishTripCreatedAsync(
            TripCreatedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishTripStatusChangedAsync(
            TripStatusChangedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishCustomerReadinessReportedAsync(
            CustomerReadinessReportedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishTripPaymentPendingAsync(
            TripPaymentPendingEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishTripPaymentSucceededAsync(
            TripPaymentSucceededEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishSOSTriggeredAsync(
            SOSTriggeredEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishBookingDriverAssignedAsync(
            BookingDriverAssignedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishDriverOfferCreatedAsync(
            DriverOfferCreatedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishDriverOfferReceivedAsync(
            DriverOfferReceivedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishDriverOfferRejectedAsync(
            DriverOfferRejectedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishDriverOfferAcceptedAsync(
            DriverOfferAcceptedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishDriverOfferExpiredAsync(
            DriverOfferExpiredEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishDriverOfferCancelledAsync(
            DriverOfferCancelledEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishDriverMatchedAsync(
            DriverMatchedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishCustomerConfirmedDriverOfferAsync(
            CustomerConfirmedDriverOfferEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishBookingSearchRadiusExpandedAsync(
            BookingSearchRadiusExpandedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishBookingExpiredAsync(
            BookingExpiredEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class OptionsMonitorFake<TOptions>(TOptions currentValue)
        : IOptionsMonitor<TOptions>
    {
        public TOptions CurrentValue { get; } = currentValue;

        public TOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
    }
}
