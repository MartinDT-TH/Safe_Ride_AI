using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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

    private sealed class MatchingFixture : IAsyncDisposable
    {
        private MatchingFixture(
            ApplicationDbContext dbContext,
            InMemoryRedisService redis,
            RealtimeNotificationServiceFake realtime,
            BookingMatchingService service,
            long bookingId,
            Guid driverId)
        {
            DbContext = dbContext;
            Redis = redis;
            Realtime = realtime;
            Service = service;
            BookingId = bookingId;
            DriverId = driverId;
        }

        public ApplicationDbContext DbContext { get; }
        public InMemoryRedisService Redis { get; }
        public RealtimeNotificationServiceFake Realtime { get; }
        public BookingMatchingService Service { get; }
        public long BookingId { get; }
        public Guid DriverId { get; }

        public static async Task<MatchingFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"booking-matching-{Guid.NewGuid():N}")
                .Options;
            var dbContext = new ApplicationDbContext(options);
            var redis = new InMemoryRedisService();
            var realtime = new RealtimeNotificationServiceFake();
            var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var booking = SeedBookingGraph(dbContext, customerId, driverId);
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
                new BookingLifecycleJobSchedulerFake());

            return new MatchingFixture(
                dbContext,
                redis,
                realtime,
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
                EstimatedDistanceKm = 5.2m,
                EstimatedDurationMinutes = 30,
                EstimatedFare = 72_000m,
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

        private static async Task SeedRedisAsync(
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
