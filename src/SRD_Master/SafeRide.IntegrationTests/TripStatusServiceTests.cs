using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.Bookings.Services;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.ExternalServices.PayOS;
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
        fixture.Redis.SetTripTrackingSnapshot(CreateTripTrackingSnapshot(fixture.TripId, 5_200));
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
        Assert.Equal(UtcNow, trip.EndedAt);
        Assert.Equal(5.2m, trip.ActualDistanceKm);
        Assert.Equal(5, trip.ActualDurationMinutes);
        Assert.Equal(72_000m, trip.ActualFare);
        Assert.Equal(62_000m, trip.FinalFare);
        Assert.Equal(2, fixture.Promotion.CurrentUsageCount);
        Assert.Single(trip.Booking.BookingPromotions);
        Assert.Equal(DriverWorkStatus.Busy, driver.WorkStatus);
        Assert.Null(fixture.Redis.DriverStatusValue);
        Assert.DoesNotContain(fixture.TripLiveKey, fixture.Redis.RemovedKeys);
        Assert.DoesNotContain(fixture.DriverActiveTripKey, fixture.Redis.RemovedKeys);
        Assert.Contains(RedisKeys.TripTrackingPath(fixture.TripId), fixture.Redis.RemovedKeys);
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
    public async Task EndTrip_WhenNoTrustedGps_DoesNotUseEstimatedDistanceForPayment()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.IN_PROGRESS);

        await fixture.Service.EndTripAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);
        await fixture.Service.ConfirmReturnByCustomerAsync(
            fixture.CustomerId,
            fixture.TripId,
            vehicleReturnedConfirmed: true,
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
            .Include(x => x.Payments)
            .SingleAsync(x => x.Id == fixture.TripId);
        var payment = Assert.Single(trip.Payments);

        Assert.Equal(0m, trip.ActualDistanceKm);
        Assert.Equal(30_000m, trip.ActualFare);
        Assert.Equal(20_000m, trip.FinalFare);
        Assert.Equal(20_000m, payment.Amount);
        Assert.NotEqual(trip.Booking.EstimatedDistanceKm, trip.ActualDistanceKm);
        Assert.NotEqual(trip.Booking.EstimatedFare, payment.Amount);
    }
    [Fact]
    public async Task ConfirmReturnByCustomer_CreatesAuditRecordAndMovesTripToWaitingPayment()
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
            .Include(x => x.Payments)
            .Include(x => x.ReturnConfirmations)
            .SingleAsync(x => x.Id == fixture.TripId);
        var confirmation = Assert.Single(trip.ReturnConfirmations);
        Assert.Equal(TripStatus.WAITING_PAYMENT, trip.TripStatus);
        Assert.Equal(BookingStatus.DriverAssigned, trip.Booking.BookingStatus);
        Assert.Null(trip.CompletedAt);
        Assert.Equal(2, fixture.Promotion.CurrentUsageCount);
        var payment = Assert.Single(trip.Payments);
        Assert.Equal(PaymentMethod.CASH, payment.PaymentMethod);
        Assert.Equal(PaymentStatus.Pending, payment.PaymentStatus);
        Assert.Null(payment.TransactionReference);
        Assert.Equal(62_000m, payment.Amount);
        Assert.Equal("VND", payment.Currency);
        Assert.Equal(fixture.DriverId, confirmation.DriverId);
        Assert.Equal(fixture.CustomerId, confirmation.ConfirmedByUserId);
        Assert.Equal(HandoverStatus.CustomerConfirmed, confirmation.HandoverStatus);
        Assert.Equal(UtcNow, confirmation.ConfirmedAt);
        Assert.Empty(confirmation.Evidence);
        Assert.Collection(
            fixture.Realtime.TripStatusNotifications,
            notification =>
            {
                Assert.Equal(fixture.TripId, notification.TripId);
                Assert.Equal(trip.BookingId, notification.BookingId);
                Assert.Equal(fixture.CustomerId, notification.CustomerId);
                Assert.Equal(fixture.DriverId, notification.DriverId);
                Assert.Equal(TripStatus.RETURN_CONFIRMED, notification.TripStatus);
                Assert.Equal(BookingStatus.DriverAssigned, notification.BookingStatus);
            },
            notification =>
            {
                Assert.Equal(fixture.TripId, notification.TripId);
                Assert.Equal(trip.BookingId, notification.BookingId);
                Assert.Equal(fixture.CustomerId, notification.CustomerId);
                Assert.Equal(fixture.DriverId, notification.DriverId);
                Assert.Equal(TripStatus.WAITING_PAYMENT, notification.TripStatus);
                Assert.Equal(BookingStatus.DriverAssigned, notification.BookingStatus);
            });
        var paymentPending = Assert.Single(fixture.Realtime.TripPaymentPendingNotifications);
        Assert.Equal(fixture.TripId, paymentPending.TripId);
        Assert.Equal(trip.BookingId, paymentPending.BookingId);
        Assert.Equal(fixture.CustomerId, paymentPending.CustomerId);
        Assert.Equal(fixture.DriverId, paymentPending.DriverId);
        Assert.Equal(payment.Id, paymentPending.PaymentId);
        Assert.Equal(PaymentMethod.CASH, paymentPending.PaymentMethod);
        Assert.Equal(PaymentStatus.Pending, paymentPending.PaymentStatus);
        Assert.Equal(62_000m, paymentPending.Amount);
        Assert.Equal("VND", paymentPending.Currency);
        Assert.Equal(TripStatus.WAITING_PAYMENT, paymentPending.TripStatus);
        Assert.Equal(BookingStatus.DriverAssigned, paymentPending.BookingStatus);
        Assert.Equal("Vui lòng thanh toán cho tài xế để hoàn tất chuyến đi.", paymentPending.Message);
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
    public async Task CompleteTrip_WhenPaymentSucceeded_CompletesAndIncrementsPromotionUsage()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_PAYMENT);
        fixture.DbContext.Payments.Add(new Payment
        {
            TripId = fixture.TripId,
            PaymentMethod = PaymentMethod.CASH,
            Amount = 62_000m,
            Currency = "VND",
            PaymentStatus = PaymentStatus.Success,
            PaidAt = UtcNow,
            CreatedAt = UtcNow
        });
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Service.CompleteTripAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .SingleAsync(x => x.Id == fixture.TripId);

        Assert.Equal(TripStatus.COMPLETED, trip.TripStatus);
        Assert.Equal(BookingStatus.Completed, trip.Booking.BookingStatus);
        Assert.Equal(UtcNow, trip.CompletedAt);
        Assert.Equal(3, fixture.Promotion.CurrentUsageCount);
        var notification = Assert.Single(fixture.Realtime.TripStatusNotifications);
        Assert.Equal(TripStatus.COMPLETED, notification.TripStatus);
        Assert.Equal(BookingStatus.Completed, notification.BookingStatus);
        Assert.Empty(fixture.Realtime.TripPaymentPendingNotifications);
        Assert.Empty(fixture.Realtime.TripPaymentSucceededNotifications);
        Assert.Single(fixture.Realtime.BookingStatusNotifications);
    }

    [Fact]
    public async Task ConfirmCashPayment_CompletesTripPublishesPaymentSucceededAndIncrementsPromotionUsage()
    {
        using var fixture = await TripStatusFixture.CreateAsync(TripStatus.WAITING_PAYMENT);
        fixture.DbContext.Payments.Add(new Payment
        {
            TripId = fixture.TripId,
            PaymentMethod = PaymentMethod.CASH,
            Amount = 62_000m,
            Currency = "VND",
            PaymentStatus = PaymentStatus.Pending,
            CreatedAt = UtcNow
        });
        fixture.DbContext.DriverWallets.Add(new DriverWallet
        {
            DriverId = fixture.DriverId,
            CurrentBalance = 100_000m
        });
        await fixture.DbContext.SaveChangesAsync();

        var paymentService = fixture.CreatePaymentService();
        var result = await paymentService.ConfirmCashPaymentAsync(
            fixture.DriverId,
            fixture.TripId,
            CancellationToken.None);

        var trip = await fixture.DbContext.Trips
            .Include(x => x.Booking)
                .ThenInclude(x => x.BookingPromotions)
                    .ThenInclude(x => x.Promotion)
            .Include(x => x.Payments)
            .SingleAsync(x => x.Id == fixture.TripId);
        var payment = Assert.Single(trip.Payments);

        Assert.Equal(PaymentStatus.Success, result.PaymentStatus);
        Assert.Equal(TripStatus.COMPLETED, result.TripStatus);
        Assert.Equal(TripStatus.COMPLETED, trip.TripStatus);
        Assert.Equal(BookingStatus.Completed, trip.Booking.BookingStatus);
        Assert.Equal(3, fixture.Promotion.CurrentUsageCount);
        Assert.Equal(PaymentStatus.Success, payment.PaymentStatus);
        Assert.Equal(PaymentMethod.CASH, payment.PaymentMethod);

        var succeeded = Assert.Single(fixture.Realtime.TripPaymentSucceededNotifications);
        Assert.Equal(fixture.TripId, succeeded.TripId);
        Assert.Equal(trip.BookingId, succeeded.BookingId);
        Assert.Equal(fixture.CustomerId, succeeded.CustomerId);
        Assert.Equal(fixture.DriverId, succeeded.DriverId);
        Assert.Equal(payment.Id, succeeded.PaymentId);
        Assert.Equal(PaymentMethod.CASH, succeeded.PaymentMethod);
        Assert.Equal(PaymentStatus.Success, succeeded.PaymentStatus);
        Assert.Equal(62_000m, succeeded.Amount);
        Assert.Equal("VND", succeeded.Currency);
        Assert.Equal(TripStatus.COMPLETED, succeeded.TripStatus);
        Assert.Equal(BookingStatus.Completed, succeeded.BookingStatus);
        Assert.Equal("Thanh toán đã hoàn tất.", succeeded.Message);
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

    private static TripTrackingSnapshot CreateTripTrackingSnapshot(
        long tripId,
        double distanceMeters)
    {
        var firstPoint = new TripTrackingPoint(
            tripId,
            10.762622,
            106.660172,
            new DateTimeOffset(UtcNow.AddMinutes(-5)).ToUnixTimeMilliseconds(),
            new DateTimeOffset(UtcNow.AddMinutes(-5)).ToUnixTimeMilliseconds(),
            UtcNow.AddMinutes(-5));
        var lastPoint = new TripTrackingPoint(
            tripId,
            10.818797,
            106.651856,
            new DateTimeOffset(UtcNow).ToUnixTimeMilliseconds(),
            new DateTimeOffset(UtcNow).ToUnixTimeMilliseconds(),
            UtcNow);

        return new TripTrackingSnapshot(
            [firstPoint, lastPoint],
            distanceMeters,
            firstPoint,
            lastPoint,
            UtcNow.AddMinutes(-5),
            UtcNow);
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

        public PayOsPaymentService CreatePaymentService()
        {
            return new PayOsPaymentService(
                new HttpClient(),
                DbContext,
                Service,
                Realtime,
                Options.Create(new PayOsOptions()));
        }

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
                new TripSharingServiceFake(),
                new OptionsMonitorFake<TripTrackingOptions>(new TripTrackingOptions()),
                new NoOpMapRoutingService(),
                new TripFareFinalizationService(new FareEstimationService()),
                NullLogger<TripStatusService>.Instance);

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
            var pricingRule = new PricingRule
            {
                ServiceType = serviceType,
                VehicleClass = RequiredLicenseClass.A1,
                BaseFare = 20_000m,
                MinFare = 30_000m,
                PricePerKm = 10_000m,
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
                PricingRule = pricingRule,
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
        private TripTrackingSnapshot _tripTrackingSnapshot = new([], 0, null, null, null, null);

        public List<string> RemovedKeys { get; } = [];
        public string? DriverStatusValue { get; private set; }

        public void SetTripTrackingSnapshot(TripTrackingSnapshot snapshot)
        {
            _tripTrackingSnapshot = snapshot;
        }

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

        public Task<TripTrackingUpdateResult> RecordTripTrackingPointAsync(
            TripTrackingPoint point,
            TripTrackingWriteOptions options,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new TripTrackingUpdateResult(true, true, 0, 0, "accepted"));

        public Task<TripTrackingSnapshot> GetTripTrackingSnapshotAsync(
            long tripId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_tripTrackingSnapshot);

        public Task RemoveTripTrackingAsync(
            long tripId,
            CancellationToken cancellationToken = default)
        {
            RemovedKeys.AddRange(RedisKeys.TripTrackingKeys(tripId));
            return Task.CompletedTask;
        }
    }

    private sealed class RealtimeNotificationServiceFake
        : IRealtimeNotificationService
    {
        public List<TripStatusChangedEvent> TripStatusNotifications { get; } = [];
        public List<TripPaymentPendingEvent> TripPaymentPendingNotifications { get; } = [];
        public List<TripPaymentSucceededEvent> TripPaymentSucceededNotifications { get; } = [];
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

        public Task PublishTripPaymentPendingAsync(
            TripPaymentPendingEvent notification,
            CancellationToken cancellationToken = default)
        {
            TripPaymentPendingNotifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task PublishTripPaymentSucceededAsync(
            TripPaymentSucceededEvent notification,
            CancellationToken cancellationToken = default)
        {
            TripPaymentSucceededNotifications.Add(notification);
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

    private sealed class NoOpMapRoutingService : IMapRoutingService
    {
        public Task<RouteEstimateResult> GetRouteEstimateAsync(
            RouteEstimateRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new RouteEstimateResult
            {
                Provider = MapProvider.Auto,
                DistanceMeters = 0,
                DurationSeconds = 0
            });
        }
    }
}
