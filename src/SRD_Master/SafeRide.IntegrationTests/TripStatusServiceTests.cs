using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Bookings;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using SafeRide.Infrastructure.Services;

namespace SafeRide.IntegrationTests;

public sealed class TripStatusServiceTests
{
    private static readonly DateTime UtcNow =
        new(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc);
    [Fact]
    public async Task EndTrip_MovesTripToWaitingReturnConfirmation()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.IN_PROGRESS);
        await fixture.Service.EndTripAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .SingleAsync(x => x.Id == fixture.TripId);
        var driver = await fixture.DbContext.DriverProfiles
            .SingleAsync(x => x.DriverId == fixture.DriverId);

        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, trip.TripStatus);
        Assert.Equal(BookingStatus.DriverAssigned, trip.Booking.BookingStatus);
        Assert.Null(trip.CompletedAt);
        Assert.Equal(2, fixture.Promotion.CurrentUsageCount);
        Assert.Single(trip.Booking.BookingPromotions);
        Assert.Equal(DriverWorkStatus.Busy, driver.WorkStatus);
        Assert.Null(fixture.Redis.DriverStatusValue);
        Assert.DoesNotContain(fixture.TripLiveKey, fixture.Redis.RemovedKeys);
        Assert.DoesNotContain(fixture.DriverActiveTripKey, fixture.Redis.RemovedKeys);
        var notification = Assert.Single(fixture.Realtime.TripStatusNotifications);
        Assert.Equal(fixture.TripId, notification.TripId);
        Assert.Equal(trip.BookingId, notification.BookingId);
        Assert.Equal(fixture.CustomerId, notification.CustomerId);
        Assert.Equal(fixture.DriverId, notification.DriverId);
        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, notification.TripStatus);
        Assert.Equal(BookingStatus.DriverAssigned, notification.BookingStatus);
        Assert.Empty(fixture.Realtime.BookingStatusNotifications);
    }
    [Fact]
    public async Task ConfirmReturnByCustomer_CreatesAuditRecordAndMovesTripToReturnConfirmed()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_RETURN_CONFIRM);
        await fixture.Service.ConfirmReturnByCustomerAsync(
            fixture.CustomerId,
            fixture.TripId,
            vehicleReturnedConfirmed: true,
            CancellationToken.None);
        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .Include(x => x.ReturnConfirmations)
            .SingleAsync(x => x.Id == fixture.TripId);
        var confirmation = Assert.Single(trip.ReturnConfirmations);
        Assert.Equal(TripStatus.RETURN_CONFIRMED, trip.TripStatus);
        Assert.Equal(BookingStatus.DriverAssigned, trip.Booking.BookingStatus);
        Assert.Null(trip.CompletedAt);
        Assert.Equal(2, fixture.Promotion.CurrentUsageCount);
        Assert.Equal(fixture.DriverId, confirmation.DriverId);
        Assert.Equal(fixture.CustomerId, confirmation.ConfirmedByUserId);
        Assert.Equal(HandoverStatus.CustomerConfirmed, confirmation.HandoverStatus);
        Assert.Equal(UtcNow, confirmation.ConfirmedAt);
        Assert.Empty(confirmation.Evidence);
        var notification = Assert.Single(fixture.Realtime.TripStatusNotifications);
        Assert.Equal(fixture.TripId, notification.TripId);
        Assert.Equal(trip.BookingId, notification.BookingId);
        Assert.Equal(fixture.CustomerId, notification.CustomerId);
        Assert.Equal(fixture.DriverId, notification.DriverId);
        Assert.Equal(TripStatus.RETURN_CONFIRMED, notification.TripStatus);
        Assert.Equal(BookingStatus.DriverAssigned, notification.BookingStatus);
        Assert.Empty(fixture.Realtime.BookingStatusNotifications);
        Assert.DoesNotContain(fixture.TripLiveKey, fixture.Redis.RemovedKeys);
        Assert.DoesNotContain(fixture.DriverActiveTripKey, fixture.Redis.RemovedKeys);
    }
    [Fact]
    public async Task ConfirmReturnByCustomer_WhenVehicleReturnNotConfirmed_RejectsRequest()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_RETURN_CONFIRM);
        var exception = await Assert.ThrowsAsync<BookingException>(
            () => fixture.Service.ConfirmReturnByCustomerAsync(
                fixture.CustomerId,
                fixture.TripId,
                vehicleReturnedConfirmed: false,
                CancellationToken.None));
        var trip = await fixture.DbContext.Trips
            .Include(x => x.ReturnConfirmations)
            .SingleAsync(x => x.Id == fixture.TripId);
        Assert.Equal("trip.return_confirmation_required", exception.Code);
        Assert.Equal(400, exception.StatusCode);
        Assert.Equal(TripStatus.WAITING_RETURN_CONFIRM, trip.TripStatus);
        Assert.Empty(trip.ReturnConfirmations);
        Assert.Empty(fixture.Realtime.TripStatusNotifications);
    }
    [Fact]
    public async Task CancelTrip_RemovesPromotionWithoutIncrementingUsageAndReleasesDriver()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.ACCEPTED);

        await fixture.Service.UpdateDriverTripStatusAsync(
            fixture.DriverId,
            fixture.TripId,
            TripStatus.CANCELLED,
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
            .SingleAsync(x => x.Id == fixture.TripId);
        var driver = await fixture.DbContext.DriverProfiles
            .SingleAsync(x => x.DriverId == fixture.DriverId);

        Assert.Equal(TripStatus.CANCELLED, trip.TripStatus);
        Assert.Equal(BookingStatus.Cancelled, trip.Booking.BookingStatus);
        Assert.Equal(fixture.DriverId, trip.CancelledByUserId);
        Assert.Equal(2, fixture.Promotion.CurrentUsageCount);
        Assert.Empty(trip.Booking.BookingPromotions);
        Assert.Equal(DriverWorkStatus.Online, driver.WorkStatus);
        Assert.Equal(DriverWorkStatus.Online.ToString(), fixture.Redis.DriverStatusValue);
        Assert.Contains(fixture.TripLiveKey, fixture.Redis.RemovedKeys);
        Assert.Contains(fixture.DriverActiveTripKey, fixture.Redis.RemovedKeys);
    }

    [Fact]
    public async Task EndTrip_WhenTripNotInProgress_RejectsInvalidTransition()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.ACCEPTED);
        var exception = await Assert.ThrowsAsync<BookingException>(
            () => fixture.Service.EndTripAsync(
                fixture.DriverId,
                fixture.TripId,
                CancellationToken.None));

        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .SingleAsync(x => x.Id == fixture.TripId);

        Assert.Equal("trip.invalid_status_transition", exception.Code);
        Assert.Equal(TripStatus.ACCEPTED, trip.TripStatus);
        Assert.Equal(BookingStatus.DriverAssigned, trip.Booking.BookingStatus);
        Assert.Equal(2, fixture.Promotion.CurrentUsageCount);
        Assert.Empty(fixture.Realtime.TripStatusNotifications);
        Assert.Empty(fixture.Redis.RemovedKeys);
    }

    private sealed class TripStatusFixture : IDisposable
    {
        private TripStatusFixture(
            ApplicationDbContext dbContext,
            TrackingRedisService redis,
            RealtimeNotificationServiceFake realtime,
            TripStatusService service,
            Guid customerId,
            Guid driverId,
            long tripId,
            Promotion promotion)
        {
            DbContext = dbContext;
            Redis = redis;
            Realtime = realtime;
            Service = service;
            CustomerId = customerId;
            DriverId = driverId;
            TripId = tripId;
            Promotion = promotion;
        }

        public ApplicationDbContext DbContext { get; }
        public TrackingRedisService Redis { get; }
        public RealtimeNotificationServiceFake Realtime { get; }
        public TripStatusService Service { get; }
        public Guid CustomerId { get; }
        public Guid DriverId { get; }
        public long TripId { get; }
        public Promotion Promotion { get; }
        public string TripLiveKey => RedisKeys.TripLive(TripId);
        public string DriverActiveTripKey => RedisKeys.DriverActiveTrip(DriverId);

        public static async Task<TripStatusFixture> CreateAsync(
            TripStatus initialTripStatus)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"trip-status-{Guid.NewGuid():N}")
                .Options;
            var dbContext = new ApplicationDbContext(options);

            var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
            var booking = SeedTripGraph(dbContext, customerId, driverId, initialTripStatus);
            await dbContext.SaveChangesAsync();

            var redis = new TrackingRedisService();
            var realtime = new RealtimeNotificationServiceFake();
            var service = new TripStatusService(
                dbContext,
                new DateTimeProviderFake(UtcNow),
                redis,
                realtime,
                new NoOpTripReturnEvidenceStorage(),
                new OptionsMonitorFake<TripTrackingOptions>(new TripTrackingOptions()));

            return new TripStatusFixture(
                dbContext,
                redis,
                realtime,
                service,
                customerId,
                driverId,
                booking.Trip!.Id,
                booking.BookingPromotions.Single().Promotion);
        }

        public void Dispose()
        {
            DbContext.Dispose();
        }

        private static Booking SeedTripGraph(
            ApplicationDbContext dbContext,
            Guid customerId,
            Guid driverId,
            TripStatus initialTripStatus)
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
            var promotion = new Promotion
            {
                PromotionCode = "SAFE10",
                DiscountType = DiscountType.Fixed,
                DiscountValue = 10_000m,
                StartDate = UtcNow.AddDays(-1),
                EndDate = UtcNow.AddDays(1),
                MaxUsageCount = 100,
                CurrentUsageCount = 2,
                MinimumOrderValue = 0,
                MaximumDiscountValue = 10_000m,
                UsageLimitPerUser = 1,
                IsActive = true
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
            booking.BookingPromotions.Add(new BookingPromotion
            {
                Booking = booking,
                Promotion = promotion,
                DiscountAmount = 10_000m,
                CreatedAt = UtcNow
            });
            booking.Trip = new Trip
            {
                Booking = booking,
                DriverId = driverId,
                TripStatus = initialTripStatus,
                DriverAssignedAt = UtcNow.AddMinutes(-10),
                StartedAt = initialTripStatus == TripStatus.IN_PROGRESS
                    ? UtcNow.AddMinutes(-5)
                    : null,
                CreatedAt = UtcNow.AddMinutes(-10)
            };
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

            return booking;
        }
    }

    private sealed class DateTimeProviderFake : IDateTimeProvider
    {
        public DateTimeProviderFake(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }

    private sealed class TrackingRedisService : IRedisService
    {
        public List<string> RemovedKeys { get; } = [];
        public string? DriverStatusValue { get; private set; }

        public Task SetAsync(
            string key,
            string value,
            TimeSpan expiration)
        {
            if (key.StartsWith("sr:driver:status:", StringComparison.Ordinal))
            {
                DriverStatusValue = value;
            }

            return Task.CompletedTask;
        }

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
                keys
                    .Distinct(StringComparer.Ordinal)
                    .ToDictionary(key => key, _ => (string?)null));

        public Task RemoveAsync(string key)
        {
            RemovedKeys.Add(key);
            return Task.CompletedTask;
        }

        public Task<long> IncrementAsync(
            string key,
            TimeSpan expiration) =>
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
    }

    private sealed class RealtimeNotificationServiceFake
        : IRealtimeNotificationService
    {
        public List<TripStatusChangedEvent> TripStatusNotifications { get; } = [];
        public List<BookingStatusChangedEvent> BookingStatusNotifications { get; } = [];

        public Task PublishBookingStatusChangedAsync(
            BookingStatusChangedEvent notification,
            CancellationToken cancellationToken = default)
        {
            BookingStatusNotifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task PublishTripStatusChangedAsync(
            TripStatusChangedEvent notification,
            CancellationToken cancellationToken = default)
        {
            TripStatusNotifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task PublishBookingSearchingStartedAsync(
            BookingSearchingStartedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishTripCreatedAsync(
            TripCreatedEvent notification,
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

    private sealed class OptionsMonitorFake<TOptions>(TOptions currentValue) : IOptionsMonitor<TOptions>
    {
        public TOptions CurrentValue { get; } = currentValue;
        public TOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
    }
    private sealed class NoOpTripReturnEvidenceStorage : ITripReturnEvidenceStorage
    {
        public Task<StoredReturnEvidenceFile> SaveAsync(
            long tripId,
            int displayOrder,
            string originalFileName,
            string contentType,
            Stream content,
            CancellationToken cancellationToken)
        {
            // Test stub: returns a fake URL; no real upload is performed.
            return Task.FromResult(new StoredReturnEvidenceFile(
                $"https://fake.cloudinary.com/trip-{tripId}/photo-{displayOrder}.jpg",
                $"fake-public-id-{displayOrder}",
                originalFileName,
                contentType,
                0L));
        }
    }
}
