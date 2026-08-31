using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Drivers.Services;
using SafeRide.Application.Features.Vehicles.Services;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using SafeRide.Infrastructure.Services;

namespace SafeRide.IntegrationTests;

[Trait(SqlServerTestDatabase.ProviderTraitName, SqlServerTestDatabase.InMemoryProvider)]
public sealed class BookingMatchingServiceTests
{
    private static readonly DateTime UtcNow =
        new(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ConcurrentStartMatchingAsync_ForSameBooking_CreatesAtMostOneActiveOffer()
    {
        await using var fixture = await MatchingFixture.CreateAsync();

        await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => fixture.Service.StartMatchingAsync(
                fixture.BookingId,
                CancellationToken.None)));

        var offers = await fixture.DbContext.BookingDriverOffers
            .Where(x => x.BookingId == fixture.BookingId
                && x.OfferStatus == DriverOfferStatus.Sent)
            .ToListAsync();
        Assert.Single(offers);
        Assert.Equal(1, fixture.Realtime.DriverOfferReceivedCount);
    }

    [Fact]
    public async Task StartMatchingAsync_ExistingActiveOfferAfterLock_DoesNotCreateNewOffer()
    {
        await using var fixture = await MatchingFixture.CreateAsync();
        fixture.DbContext.BookingDriverOffers.Add(new BookingDriverOffer
        {
            BookingId = fixture.BookingId,
            DriverId = fixture.DriverId,
            OfferStatus = DriverOfferStatus.Sent,
            OfferedAt = UtcNow,
            ExpiresAt = UtcNow.AddSeconds(30)
        });
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Service.StartMatchingAsync(
            fixture.BookingId,
            CancellationToken.None);

        var offerCount = await fixture.DbContext.BookingDriverOffers
            .CountAsync(x => x.BookingId == fixture.BookingId);
        Assert.Equal(1, offerCount);
        Assert.Equal(0, fixture.Realtime.DriverOfferReceivedCount);
    }

    [Theory]
    [InlineData(DriverOfferStatus.Sent)]
    [InlineData(DriverOfferStatus.DriverAccepted)]
    public async Task StartMatchingAsync_ExpiredOfferForAnotherBooking_DoesNotBlockDriver(
        DriverOfferStatus expiredOfferStatus)
    {
        await using var fixture = await MatchingFixture.CreateAsync();
        fixture.DbContext.BookingDriverOffers.Add(new BookingDriverOffer
        {
            // The matching predicate only needs a different historical booking row.
            // The InMemory provider intentionally does not enforce relational foreign keys.
            BookingId = fixture.BookingId + 1,
            DriverId = fixture.DriverId,
            OfferStatus = expiredOfferStatus,
            OfferedAt = UtcNow.AddMinutes(-2),
            ExpiresAt = UtcNow.AddSeconds(-1)
        });
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.Service.StartMatchingAsync(
            fixture.BookingId,
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, fixture.Route.RequestCount);
        Assert.Single(await fixture.DbContext.BookingDriverOffers
            .Where(x => x.BookingId == fixture.BookingId)
            .ToListAsync());
    }

    [Fact]
    public async Task StartMatchingAsync_PersistentOfflineDriver_DoesNotReceiveOfferFromStaleRedisState()
    {
        await using var fixture = await MatchingFixture.CreateAsync();
        var profile = await fixture.DbContext.DriverProfiles.FindAsync(fixture.DriverId);
        profile!.WorkStatus = DriverWorkStatus.Offline;
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.Service.StartMatchingAsync(
            fixture.BookingId,
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, fixture.Route.RequestCount);
        Assert.Empty(await fixture.DbContext.BookingDriverOffers.ToListAsync());
    }

    [Fact]
    public async Task StartMatchingAsync_ReleasesBookingLockAfterMatchingAttempt()
    {
        await using var fixture = await MatchingFixture.CreateAsync();

        await fixture.Service.StartMatchingAsync(
            fixture.BookingId,
            CancellationToken.None);

        var acquired = await fixture.Redis.TryAcquireDistributedLockAsync(
            RedisKeys.MatchingBookingLock(fixture.BookingId),
            "probe",
            TimeSpan.FromSeconds(30));
        Assert.True(acquired);
    }

    [Fact]
    public async Task StartMatchingAsync_PersistsAuthoritativePickupSnapshotAndCompensation()
    {
        await using var fixture = await MatchingFixture.CreateAsync();
        fixture.Route.DistanceMeters = 7_500;

        await fixture.Service.StartMatchingAsync(fixture.BookingId, CancellationToken.None);

        var offer = Assert.Single(fixture.DbContext.BookingDriverOffers);
        Assert.Equal(7.5m, offer.PickupDistanceKm);
        Assert.Equal(7_500m, offer.LongPickupCompensation);
        Assert.Equal(1, fixture.Route.RequestCount);
        Assert.Equal("DriverMatchingPickupEligibility", fixture.Route.LastRequest?.RequestSource);
    }

    [Fact]
    public async Task StartMatchingAsync_PreservesNearestFirstRedisGeoOrderAfterSqlFiltering()
    {
        await using var fixture = await MatchingFixture.CreateAsync();
        var nearestId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var nearestUser = new AspNetUser
        {
            Id = nearestId,
            UserName = "nearest@example.test",
            Email = "nearest@example.test",
            FullName = "Nearest Driver",
            IsActive = true,
            CreatedAt = UtcNow
        };
        fixture.DbContext.AspNetUsers.Add(nearestUser);
        fixture.DbContext.DriverProfiles.Add(new DriverProfile
        {
            DriverId = nearestId,
            Driver = nearestUser,
            IdentityCardNumber = "987654321",
            WorkStatus = DriverWorkStatus.Online,
            LastActiveAt = UtcNow,
            CreatedAt = UtcNow.AddDays(-1)
        });
        fixture.DbContext.DriverKycs.Add(new DriverKyc
        {
            DriverId = nearestId,
            Driver = nearestUser,
            DocumentType = KycDocumentType.DRIVING_LICENSE,
            KycStatus = KycStatus.Approved,
            LicenseClass = LicenseClass.A1,
            DocumentNumber = "A987654",
            CreatedAt = UtcNow.AddDays(-1),
            VerifiedAt = UtcNow.AddHours(-1)
        });
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Redis.GeoAddAsync(
            RedisKeys.OnlineDriversGeo, 106.70, 10.80, fixture.DriverId.ToString());
        await MatchingFixture.SeedRedisAsync(fixture.Redis, nearestId);

        await fixture.Service.StartMatchingAsync(fixture.BookingId, CancellationToken.None);

        Assert.Equal(nearestId, Assert.Single(fixture.DbContext.BookingDriverOffers).DriverId);
    }

    [Fact]
    public async Task StartMatchingAsync_LongPickupAboveOptIn_RequiresPreference()
    {
        await using var fixture = await MatchingFixture.CreateAsync();
        fixture.Route.DistanceMeters = 8_001;

        var result = await fixture.Service.StartMatchingAsync(fixture.BookingId, CancellationToken.None);

        Assert.Null(result);
        Assert.Empty(fixture.DbContext.BookingDriverOffers);

        var profile = await fixture.DbContext.DriverProfiles.FindAsync(fixture.DriverId);
        profile!.AcceptLongPickupTrips = true;
        await fixture.DbContext.SaveChangesAsync();

        result = await fixture.Service.StartMatchingAsync(fixture.BookingId, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(2, fixture.Route.RequestCount);
    }

    [Fact]
    public async Task StartMatchingAsync_LongDistanceAboveOptIn_RequiresPreference_AndMaximumBlocks()
    {
        await using var fixture = await MatchingFixture.CreateAsync(30.001m);

        Assert.Null(await fixture.Service.StartMatchingAsync(fixture.BookingId, CancellationToken.None));
        Assert.Equal(0, fixture.Route.RequestCount);

        var profile = await fixture.DbContext.DriverProfiles.FindAsync(fixture.DriverId);
        profile!.AcceptLongDistanceTrips = true;
        await fixture.DbContext.SaveChangesAsync();
        Assert.NotNull(await fixture.Service.StartMatchingAsync(fixture.BookingId, CancellationToken.None));

        await using var maximumFixture = await MatchingFixture.CreateAsync(50.001m);
        Assert.Null(await maximumFixture.Service.StartMatchingAsync(maximumFixture.BookingId, CancellationToken.None));
        Assert.Equal(0, maximumFixture.Route.RequestCount);
    }

    [Fact]
    public async Task StartMatchingAsync_HourlyBooking_IsExemptFromTripDistanceEligibility()
    {
        await using var fixture = await MatchingFixture.CreateAsync(80m, isHourly: true);

        Assert.NotNull(await fixture.Service.StartMatchingAsync(fixture.BookingId, CancellationToken.None));
    }

    private sealed class MatchingFixture : IAsyncDisposable
    {
        private MatchingFixture(
            ApplicationDbContext dbContext,
            InMemoryRedisService redis,
            RealtimeNotificationServiceFake realtime,
            RouteMapFake route,
            BookingMatchingService service,
            long bookingId,
            Guid driverId)
        {
            DbContext = dbContext;
            Redis = redis;
            Realtime = realtime;
            Route = route;
            Service = service;
            BookingId = bookingId;
            DriverId = driverId;
        }

        public ApplicationDbContext DbContext { get; }
        public InMemoryRedisService Redis { get; }
        public RealtimeNotificationServiceFake Realtime { get; }
        public RouteMapFake Route { get; }
        public BookingMatchingService Service { get; }
        public long BookingId { get; }
        public Guid DriverId { get; }

        public static async Task<MatchingFixture> CreateAsync(
            decimal estimatedDistanceKm = 5.2m,
            bool isHourly = false)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"booking-matching-{Guid.NewGuid():N}")
                .Options;
            var dbContext = new ApplicationDbContext(options, new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
            var redis = new InMemoryRedisService();
            var realtime = new RealtimeNotificationServiceFake();
            var route = new RouteMapFake();
            var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var booking = SeedBookingGraph(
                dbContext,
                customerId,
                driverId,
                estimatedDistanceKm,
                isHourly);
            await dbContext.SaveChangesAsync();
            await SeedRedisAsync(redis, driverId);
            var policyProvider = new MatchingPolicyProviderFake();
            var service = new BookingMatchingService(
                NullLogger<BookingMatchingService>.Instance,
                dbContext,
                new LicenseCompatibilityService(),
                new VehicleLicenseRequirementService(),
                new DateTimeProviderFake(UtcNow),
                redis,
                realtime,
                policyProvider,
                new BookingLifecycleJobSchedulerFake(),
                route,
                Options.Create(new DriverCompensationOptions
                {
                    LongPickupThresholdKm = 5,
                    LongPickupOptInThresholdKm = 8,
                    LongDistanceThresholdKm = 15,
                    LongDistanceOptInThresholdKm = 30,
                    MaximumTripDistanceKm = 50,
                    LongPickupRatePerKm = 3_000m,
                    LongDistanceRatePerKm = 3_000m
                }));

            return new MatchingFixture(
                dbContext,
                redis,
                realtime,
                route,
                service,
                booking.BookingId,
                driverId);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
        }

        private static Booking SeedBookingGraph(
            ApplicationDbContext dbContext,
            Guid customerId,
            Guid driverId,
            decimal estimatedDistanceKm,
            bool isHourly)
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
                BookingId = 100,
                CustomerId = customerId,
                Customer = customer,
                Vehicle = vehicle,
                BookingType = BookingType.Now,
                BookingStatus = BookingStatus.Searching,
                PickupAddress = "Pickup",
                PickupLocation = new Point(106.660172, 10.762622) { SRID = 4326 },
                DestinationAddress = "Destination",
                DestinationLocation = new Point(106.651856, 10.818797) { SRID = 4326 },
                EstimatedDistanceKm = estimatedDistanceKm,
                EstimatedDurationMinutes = 30,
                EstimatedFare = 72_000m,
                AcceptedPricePerKm = isHourly ? null : 10_000m,
                AcceptedPricePerHour = isHourly ? 100_000m : null,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            };
            var driver = new DriverProfile
            {
                DriverId = driverId,
                Driver = driverUser,
                IdentityCardNumber = "123456789",
                WorkStatus = DriverWorkStatus.Online,
                LastActiveAt = UtcNow,
                ExperienceYears = 2,
                CreatedAt = UtcNow.AddDays(-1)
            };
            var kyc = new DriverKyc
            {
                DriverId = driverId,
                Driver = driverUser,
                DocumentType = KycDocumentType.DRIVING_LICENSE,
                KycStatus = KycStatus.Approved,
                LicenseClass = LicenseClass.A1,
                DocumentNumber = "A123456",
                CreatedAt = UtcNow.AddDays(-1),
                VerifiedAt = UtcNow.AddHours(-1)
            };

            dbContext.AspNetUsers.AddRange(customer, driverUser);
            dbContext.DriverProfiles.Add(driver);
            dbContext.DriverKycs.Add(kyc);
            dbContext.Bookings.Add(booking);

            return booking;
        }

        internal static async Task SeedRedisAsync(
            InMemoryRedisService redis,
            Guid driverId)
        {
            await redis.SetAsync(
                RedisKeys.DriverOnline(driverId),
                "1",
                TimeSpan.FromMinutes(5));
            await redis.SetAsync(
                RedisKeys.DriverStatus(driverId),
                DriverWorkStatus.Online.ToString(),
                TimeSpan.FromMinutes(5));
            await redis.SetAsync(
                RedisKeys.DriverLocation(driverId),
                System.Text.Json.JsonSerializer.Serialize(new DriverLocationCache(
                    driverId,
                    10.762622,
                    106.660172,
                    UtcNow)),
                TimeSpan.FromMinutes(5));
            await redis.GeoAddAsync(
                RedisKeys.OnlineDriversGeo,
                106.660172,
                10.762622,
                driverId.ToString());
        }
    }

    private sealed class DateTimeProviderFake(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class RouteMapFake : IMapRoutingService
    {
        public double DistanceMeters { get; set; } = 5_000;
        public int RequestCount { get; private set; }
        public RouteEstimateRequest? LastRequest { get; private set; }

        public Task<RouteEstimateResult> GetRouteEstimateAsync(
            RouteEstimateRequest request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequest = request;
            return Task.FromResult(new RouteEstimateResult
            {
                Provider = MapProvider.Auto,
                DistanceMeters = DistanceMeters,
                DurationSeconds = 600
            });
        }
    }

    private sealed class MatchingPolicyProviderFake : IMatchingPolicyProvider
    {
        public MatchingOptions Current { get; } = new()
        {
            CandidateLimit = 10,
            InitialRadiusKm = 5,
            OfferExpireSeconds = 30,
            MatchingTickSeconds = 10,
            BookingExpireAfterMinutes = 10
        };

        public DateTime? GetMatchingStartedAt(Booking booking) => booking.CreatedAt;

        public BookingMatchingSnapshot GetSnapshot(Booking booking, DateTime utcNow)
        {
            var expiresAt = booking.CreatedAt.AddMinutes(Current.BookingExpireAfterMinutes);
            return new BookingMatchingSnapshot(
                Current.InitialRadiusKm,
                expiresAt,
                Math.Max(0, (int)Math.Ceiling((expiresAt - utcNow).TotalSeconds)),
                "SafeRide dang tim tai xe.",
                false);
        }
    }

    private sealed class BookingLifecycleJobSchedulerFake : IBookingLifecycleJobScheduler
    {
        public void ScheduleExpandRadius(long bookingId, TimeSpan delay) { }

        public void ScheduleExpireBooking(long bookingId, TimeSpan delay) { }

        public void ScheduleExpireDriverOffer(long offerId, TimeSpan delay) { }

        public Task CancelExpireDriverOfferAsync(
            long offerId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task CancelJobsForBookingAsync(
            long bookingId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RealtimeNotificationServiceFake : IRealtimeNotificationService
    {
        public int DriverOfferReceivedCount { get; private set; }

        public Task PublishDriverOfferReceivedAsync(
            DriverOfferReceivedEvent notification,
            CancellationToken cancellationToken = default)
        {
            DriverOfferReceivedCount++;
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

        public Task PublishDriverLocationUpdatedAsync(
            DriverLocationUpdatedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishDriverOfferCreatedAsync(
            DriverOfferCreatedEvent notification,
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
}
