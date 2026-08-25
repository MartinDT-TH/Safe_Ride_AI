using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Bookings.DTOs;
using SafeRide.Application.Features.Drivers.Services;
using SafeRide.Application.Features.Vehicles.Services;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Repositories;
using SafeRide.Infrastructure.Redis;
using SafeRide.Infrastructure.Services;

namespace SafeRide.IntegrationTests;

[Trait(SqlServerTestDatabase.ProviderTraitName, SqlServerTestDatabase.SqlServerProvider)]
public sealed class BookingAssignmentServiceTests
{
    private static readonly DateTime UtcNow =
        new(2026, 8, 10, 5, 0, 0, DateTimeKind.Utc);

    [SqlServerFact]
    public async Task AcceptDriverOfferAsync_NowBooking_WaitsForCustomerConfirmation()
    {
        await using var fixture = await Fixture.CreateAsync(BookingType.Now);
        Assert.True(fixture.DbContext.Database.CreateExecutionStrategy().RetriesOnFailure);

        var response = await fixture.Service.AcceptDriverOfferAsync(
            fixture.DriverId,
            fixture.WinningOfferId,
            CancellationToken.None);

        var offer = await fixture.DbContext.BookingDriverOffers.FindAsync(fixture.WinningOfferId);
        var driver = await fixture.DbContext.DriverProfiles.FindAsync(fixture.DriverId);
        Assert.Equal(BookingStatus.Searching, response.BookingStatus);
        Assert.Equal(DriverOfferStatus.DriverAccepted, offer!.OfferStatus);
        Assert.Null(response.TripId);
        Assert.Null(response.TripStatus);
        Assert.NotNull(response.DriverOffer);
        Assert.Equal(DriverOfferStatus.DriverAccepted, response.DriverOffer.OfferStatus);
        Assert.Empty(fixture.DbContext.Trips);
        Assert.Equal(DriverWorkStatus.Online, driver!.WorkStatus);
        Assert.Contains(fixture.WinningOfferId, fixture.Scheduler.ScheduledOfferIds);
    }

    [SqlServerFact]
    public async Task AcceptDriverOfferAsync_NowBooking_RetryIsIdempotent()
    {
        await using var fixture = await Fixture.CreateAsync(BookingType.Now);

        var first = await fixture.Service.AcceptDriverOfferAsync(
            fixture.DriverId,
            fixture.WinningOfferId,
            CancellationToken.None);
        var retry = await fixture.Service.AcceptDriverOfferAsync(
            fixture.DriverId,
            fixture.WinningOfferId,
            CancellationToken.None);

        Assert.Equal(BookingStatus.Searching, first.BookingStatus);
        Assert.Equal(BookingStatus.Searching, retry.BookingStatus);
        Assert.Equal(DriverOfferStatus.DriverAccepted, retry.DriverOffer?.OfferStatus);
        Assert.Null(retry.TripId);
        Assert.Empty(fixture.DbContext.Trips);
        Assert.Equal(
            first.DriverOffer?.ExpiresAt,
            retry.DriverOffer?.ExpiresAt);
    }

    [SqlServerFact]
    public async Task AcceptDriverOfferAsync_ScheduledBooking_AutoAssignsDriver()
    {
        await using var fixture = await Fixture.CreateAsync(BookingType.Scheduled);

        var response = await fixture.Service.AcceptDriverOfferAsync(
            fixture.DriverId,
            fixture.WinningOfferId,
            CancellationToken.None);

        var booking = await fixture.DbContext.Bookings.FindAsync(fixture.BookingId);
        var offer = await fixture.DbContext.BookingDriverOffers.FindAsync(fixture.WinningOfferId);
        var driver = await fixture.DbContext.DriverProfiles.FindAsync(fixture.DriverId);
        var trip = Assert.Single(fixture.DbContext.Trips);
        Assert.Equal(BookingStatus.DriverAssigned, booking!.BookingStatus);
        Assert.Equal(DriverOfferStatus.CustomerConfirmed, offer!.OfferStatus);
        Assert.Equal(TripStatus.ACCEPTED, trip.TripStatus);
        Assert.Equal(DriverWorkStatus.Busy, driver!.WorkStatus);
        Assert.Equal(trip.Id, response.TripId);
        Assert.Equal(TripStatus.ACCEPTED, response.TripStatus);
        Assert.NotNull(response.DriverOffer);
        Assert.Empty(fixture.Realtime.CustomerConfirmedOffers);
        Assert.Contains(
            fixture.Realtime.DriverAssignments,
            notification => notification.Message.Contains("tự động xác nhận", StringComparison.Ordinal));
    }

    [SqlServerFact]
    public async Task AcceptDriverOfferAsync_ScheduledBooking_CancelsCompetingOffers()
    {
        await using var fixture = await Fixture.CreateAsync(
            BookingType.Scheduled,
            includeCompetingOffer: true);

        await fixture.Service.AcceptDriverOfferAsync(
            fixture.DriverId,
            fixture.WinningOfferId,
            CancellationToken.None);

        var competingOffer = await fixture.DbContext.BookingDriverOffers
            .SingleAsync(x => x.Id == fixture.CompetingOfferId);
        Assert.Equal(DriverOfferStatus.Cancelled, competingOffer.OfferStatus);
        Assert.NotNull(competingOffer.CancelledAt);
        Assert.Contains(fixture.CompetingOfferId, fixture.Scheduler.CancelledOfferIds);
        Assert.Contains(
            fixture.Realtime.CancelledOffers,
            notification => notification.OfferId == fixture.CompetingOfferId);
    }

    [SqlServerFact]
    public async Task AcceptDriverOfferAsync_ScheduledBooking_CancelsLifecycleJobs()
    {
        await using var fixture = await Fixture.CreateAsync(BookingType.Scheduled);

        await fixture.Service.AcceptDriverOfferAsync(
            fixture.DriverId,
            fixture.WinningOfferId,
            CancellationToken.None);

        Assert.Equal([fixture.BookingId], fixture.Scheduler.CancelledBookingIds);
        Assert.Contains(fixture.WinningOfferId, fixture.Scheduler.CancelledOfferIds);
    }

    [SqlServerFact]
    public async Task AcceptDriverOfferAsync_ScheduledBooking_RetryDoesNotCreateDuplicateTrip()
    {
        await using var fixture = await Fixture.CreateAsync(BookingType.Scheduled);

        var first = await fixture.Service.AcceptDriverOfferAsync(
            fixture.DriverId,
            fixture.WinningOfferId,
            CancellationToken.None);
        var retry = await fixture.Service.AcceptDriverOfferAsync(
            fixture.DriverId,
            fixture.WinningOfferId,
            CancellationToken.None);

        Assert.Equal(first.TripId, retry.TripId);
        Assert.Single(fixture.DbContext.Trips);
    }

    [SqlServerFact]
    public async Task ConcurrentScheduledDriverAcceptance_OnlyOneDriverWins()
    {
        await using var fixture = await Fixture.CreateAsync(
            BookingType.Scheduled,
            includeCompetingOffer: true);
        await using var secondContext = fixture.CreateAdditionalDbContext();
        var secondService = fixture.CreateService(secondContext);

        var attempts = new[]
        {
            CaptureAsync(() => fixture.Service.AcceptDriverOfferAsync(
                fixture.DriverId,
                fixture.WinningOfferId,
                CancellationToken.None)),
            CaptureAsync(() => secondService.AcceptDriverOfferAsync(
                fixture.CompetingDriverId,
                fixture.CompetingOfferId,
                CancellationToken.None))
        };

        var results = await Task.WhenAll(attempts);
        Assert.Equal(1, results.Count(result => result.Response?.TripId is not null));
        Assert.Equal(1, await fixture.DbContext.Trips.AsNoTracking().CountAsync());
        Assert.Equal(
            1,
            await fixture.DbContext.BookingDriverOffers.AsNoTracking()
                .CountAsync(x => x.OfferStatus == DriverOfferStatus.CustomerConfirmed));
    }

    [SqlServerFact]
    public async Task ConfirmDriverAsync_NowBooking_StillWorks()
    {
        await using var fixture = await Fixture.CreateAsync(
            BookingType.Now,
            winningOfferStatus: DriverOfferStatus.DriverAccepted);

        var response = await fixture.Service.ConfirmDriverAsync(
            fixture.CustomerId,
            fixture.BookingId,
            fixture.WinningOfferId,
            CancellationToken.None);

        Assert.Equal(BookingStatus.DriverAssigned, response.BookingStatus);
        Assert.NotNull(response.TripId);
        Assert.Equal(TripStatus.ACCEPTED, response.TripStatus);
        Assert.Single(fixture.Realtime.CustomerConfirmedOffers);
    }

    [SqlServerFact]
    public async Task ConfirmDriverAsync_ScheduledBooking_DoesNotRequireCustomerConfirmation()
    {
        await using var fixture = await Fixture.CreateAsync(BookingType.Scheduled);
        var assigned = await fixture.Service.AcceptDriverOfferAsync(
            fixture.DriverId,
            fixture.WinningOfferId,
            CancellationToken.None);

        var response = await fixture.Service.ConfirmDriverAsync(
            fixture.CustomerId,
            fixture.BookingId,
            fixture.WinningOfferId,
            CancellationToken.None);

        Assert.Equal(assigned.TripId, response.TripId);
        Assert.Equal(BookingStatus.DriverAssigned, response.BookingStatus);
        Assert.Single(fixture.DbContext.Trips);
    }

    [SqlServerFact]
    public async Task GetActiveBookingAsync_AssignedScheduledBooking_IsRestorable()
    {
        await using var fixture = await Fixture.CreateAsync(BookingType.Scheduled);
        var assigned = await fixture.Service.AcceptDriverOfferAsync(
            fixture.DriverId,
            fixture.WinningOfferId,
            CancellationToken.None);

        var restored = await fixture.CreateRepository().GetActiveBookingAsync(
            fixture.CustomerId,
            CancellationToken.None);

        Assert.NotNull(restored);
        Assert.Equal(BookingType.Scheduled, restored.BookingType);
        Assert.Equal(BookingStatus.DriverAssigned, restored.BookingStatus);
        Assert.Equal(assigned.TripId, restored.Trip?.Id);
    }

    [SqlServerFact]
    public async Task GetActiveBookingAsync_PendingScheduledBooking_IsRestorable()
    {
        await using var fixture = await Fixture.CreateAsync(BookingType.Scheduled);
        var booking = await fixture.DbContext.Bookings.FindAsync(fixture.BookingId);
        booking!.BookingStatus = BookingStatus.PendingSchedule;
        await fixture.DbContext.SaveChangesAsync();

        var restored = await fixture.CreateRepository().GetActiveBookingAsync(
            fixture.CustomerId,
            CancellationToken.None);

        Assert.NotNull(restored);
        Assert.Equal(BookingType.Scheduled, restored.BookingType);
        Assert.Equal(BookingStatus.PendingSchedule, restored.BookingStatus);
        Assert.Null(restored.Trip);
    }

    private static async Task<AcceptanceResult> CaptureAsync(
        Func<Task<SafeRide.Application.Features.Bookings.Commands.CreateBooking.CreateBookingResponse>> action)
    {
        try
        {
            return new AcceptanceResult(await action(), null);
        }
        catch (Exception exception)
        {
            return new AcceptanceResult(null, exception);
        }
    }

    private sealed record AcceptanceResult(
        SafeRide.Application.Features.Bookings.Commands.CreateBooking.CreateBookingResponse? Response,
        Exception? Exception);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqlServerTestDatabase _database;
        private readonly DateTimeProviderFake _clock = new(UtcNow);
        private readonly MatchingPolicyProviderFake _matchingPolicy = new();
        private readonly RedisServiceFake _redis = new();
        private readonly BookingMatchingServiceFake _matching = new();

        private Fixture(
            SqlServerTestDatabase database,
            ApplicationDbContext dbContext)
        {
            _database = database;
            DbContext = dbContext;
            Scheduler = new BookingLifecycleJobSchedulerFake();
            Realtime = new RealtimeNotificationServiceFake();
            Service = CreateService(dbContext);
        }

        public Guid CustomerId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public Guid DriverId { get; } = Guid.Parse("22222222-2222-2222-2222-222222222222");
        public Guid CompetingDriverId { get; } = Guid.Parse("33333333-3333-3333-3333-333333333333");
        public long BookingId { get; private set; }
        public long WinningOfferId { get; private set; }
        public long CompetingOfferId { get; private set; }
        public ApplicationDbContext DbContext { get; }
        public BookingLifecycleJobSchedulerFake Scheduler { get; }
        public RealtimeNotificationServiceFake Realtime { get; }
        public BookingAssignmentService Service { get; }

        public static async Task<Fixture> CreateAsync(
            BookingType bookingType,
            bool includeCompetingOffer = false,
            DriverOfferStatus winningOfferStatus = DriverOfferStatus.Sent)
        {
            var database = await SqlServerTestDatabase.CreateAsync("BookingAssignment");
            var dbContext = database.CreateDbContext();
            try
            {
                var fixture = new Fixture(database, dbContext);
                await fixture.SeedAsync(bookingType, includeCompetingOffer, winningOfferStatus);
                return fixture;
            }
            catch
            {
                await dbContext.DisposeAsync();
                await database.DisposeAsync();
                throw;
            }
        }

        public ApplicationDbContext CreateAdditionalDbContext() =>
            _database.CreateDbContext();

        public BookingAssignmentService CreateService(ApplicationDbContext dbContext) =>
            new(
                dbContext,
                _clock,
                new LicenseCompatibilityService(),
                new VehicleLicenseRequirementService(),
                _redis,
                Realtime,
                _matching,
                _matchingPolicy,
                Scheduler,
                new OptionsMonitorFake<TripTrackingOptions>(new TripTrackingOptions()),
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

        public BookingRepository CreateRepository() =>
            new(
                DbContext,
                _redis,
                _matchingPolicy,
                new OptionsMonitorFake<TripTrackingOptions>(new TripTrackingOptions()));

        private async Task SeedAsync(
            BookingType bookingType,
            bool includeCompetingOffer,
            DriverOfferStatus winningOfferStatus)
        {
            var customer = NewUser(CustomerId, "Khách hàng");
            var driver = NewUser(DriverId, "Tài xế 1");
            var users = new List<AspNetUser> { customer, driver };
            if (includeCompetingOffer)
            {
                users.Add(NewUser(CompetingDriverId, "Tài xế 2"));
            }

            DbContext.AspNetUsers.AddRange(users);
            var serviceType = new ServiceType { ServiceName = "Thuê xe" };
            var vehicle = new Vehicle
            {
                OwnerUserId = CustomerId,
                PlateNumber = "51A-12345",
                BrandModel = "Test Car",
                VehicleType = VehicleType.Car,
                RequiredLicenseClass = RequiredLicenseClass.B,
                EngineType = EngineType.ICE,
                TransmissionType = TransmissionType.Automatic,
                CreatedAt = UtcNow
            };
            DbContext.ServiceTypes.Add(serviceType);
            DbContext.Vehicles.Add(vehicle);

            DbContext.DriverProfiles.Add(NewDriverProfile(DriverId));
            DbContext.DriverKycs.Add(NewDriverKyc(DriverId));
            if (includeCompetingOffer)
            {
                DbContext.DriverProfiles.Add(NewDriverProfile(CompetingDriverId));
                DbContext.DriverKycs.Add(NewDriverKyc(CompetingDriverId));
            }

            await DbContext.SaveChangesAsync();

            var booking = new Booking
            {
                CustomerId = CustomerId,
                VehicleId = vehicle.Id,
                ServiceTypeId = serviceType.Id,
                BookingType = bookingType,
                BookingStatus = BookingStatus.Searching,
                BookingSource = bookingType == BookingType.Scheduled
                    ? BookingSource.Scheduled
                    : BookingSource.Manual,
                PickupAddress = "Điểm đón",
                PickupLocation = new Point(106.66, 10.76) { SRID = 4326 },
                DestinationAddress = "Điểm đến",
                DestinationLocation = new Point(106.67, 10.77) { SRID = 4326 },
                EstimatedDistanceKm = 5,
                EstimatedDurationMinutes = 15,
                EstimatedFare = 100_000,
                ScheduledAt = bookingType == BookingType.Scheduled
                    ? UtcNow.AddMinutes(15)
                    : null,
                CreatedAt = UtcNow.AddHours(-1),
                UpdatedAt = UtcNow.AddMinutes(-1)
            };
            DbContext.Bookings.Add(booking);
            await DbContext.SaveChangesAsync();
            BookingId = booking.BookingId;

            var winningOffer = new BookingDriverOffer
            {
                BookingId = BookingId,
                DriverId = DriverId,
                OfferStatus = winningOfferStatus,
                OfferedAt = UtcNow.AddSeconds(-5),
                ExpiresAt = UtcNow.AddMinutes(2),
                ConfirmedAt = winningOfferStatus == DriverOfferStatus.DriverAccepted
                    ? UtcNow.AddSeconds(-1)
                    : null
            };
            DbContext.BookingDriverOffers.Add(winningOffer);
            BookingDriverOffer? competingOffer = null;
            if (includeCompetingOffer)
            {
                competingOffer = new BookingDriverOffer
                {
                    BookingId = BookingId,
                    DriverId = CompetingDriverId,
                    OfferStatus = DriverOfferStatus.Sent,
                    OfferedAt = UtcNow.AddSeconds(-4),
                    ExpiresAt = UtcNow.AddMinutes(2)
                };
                DbContext.BookingDriverOffers.Add(competingOffer);
            }

            await DbContext.SaveChangesAsync();
            WinningOfferId = winningOffer.Id;
            CompetingOfferId = competingOffer?.Id ?? 0;
            _redis.SetDriverOnline(DriverId);
            if (includeCompetingOffer)
            {
                _redis.SetDriverOnline(CompetingDriverId);
            }
        }

        private static AspNetUser NewUser(Guid id, string fullName) => new()
        {
            Id = id,
            UserName = id.ToString("N"),
            NormalizedUserName = id.ToString("N").ToUpperInvariant(),
            FullName = fullName,
            IsActive = true,
            CreatedAt = UtcNow
        };

        private static DriverProfile NewDriverProfile(Guid driverId) => new()
        {
            DriverId = driverId,
            IdentityCardNumber = driverId.ToString("N")[..12],
            WorkStatus = DriverWorkStatus.Online,
            LastActiveAt = UtcNow,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow
        };

        private static DriverKyc NewDriverKyc(Guid driverId) => new()
        {
            DriverId = driverId,
            DocumentType = KycDocumentType.DRIVING_LICENSE,
            DocumentNumber = $"B-{driverId:N}",
            LicenseClass = LicenseClass.B,
            FrontImageUrl = $"https://example.test/license-{driverId:N}.jpg",
            KycStatus = KycStatus.Approved,
            CreatedAt = UtcNow,
            VerifiedAt = UtcNow
        };

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _database.DisposeAsync();
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
            CustomerConfirmExpireSeconds = 90
        };

        public DateTime? GetMatchingStartedAt(Booking booking) => booking.UpdatedAt;

        public BookingMatchingSnapshot GetSnapshot(Booking booking, DateTime utcNow) =>
            new(null, null, null, null, false);
    }

    private sealed class BookingMatchingServiceFake : IBookingMatchingService
    {
        public Task<BookingDriverOfferDto?> StartMatchingAsync(
            long bookingId,
            CancellationToken cancellationToken) =>
            Task.FromResult<BookingDriverOfferDto?>(null);
    }

    private sealed class BookingLifecycleJobSchedulerFake : IBookingLifecycleJobScheduler
    {
        public List<long> ScheduledOfferIds { get; } = [];
        public List<long> CancelledOfferIds { get; } = [];
        public List<long> CancelledBookingIds { get; } = [];

        public void ScheduleExpandRadius(long bookingId, TimeSpan delay) { }
        public void ScheduleExpireBooking(long bookingId, TimeSpan delay) { }

        public void ScheduleExpireDriverOffer(long offerId, TimeSpan delay) =>
            ScheduledOfferIds.Add(offerId);

        public Task CancelExpireDriverOfferAsync(
            long offerId,
            CancellationToken cancellationToken = default)
        {
            CancelledOfferIds.Add(offerId);
            return Task.CompletedTask;
        }

        public Task CancelJobsForBookingAsync(
            long bookingId,
            CancellationToken cancellationToken = default)
        {
            CancelledBookingIds.Add(bookingId);
            return Task.CompletedTask;
        }
    }

    private sealed class RedisServiceFake : IRedisService
    {
        private readonly Dictionary<string, string> _values = [];

        public void SetDriverOnline(Guid driverId)
        {
            _values[RedisKeys.DriverOnline(driverId)] = "1";
            _values[RedisKeys.DriverStatus(driverId)] = DriverWorkStatus.Online.ToString();
        }

        public Task SetAsync(string key, string value, TimeSpan expiration)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan expiration)
        {
            if (_values.ContainsKey(key)) return Task.FromResult(false);
            _values[key] = value;
            return Task.FromResult(true);
        }

        public Task<bool> TryAcquireDistributedLockAsync(string key, string value, TimeSpan expiration) =>
            SetIfNotExistsAsync(key, value, expiration);

        public Task<string?> GetAsync(string key) =>
            Task.FromResult(_values.GetValueOrDefault(key));

        public Task<IReadOnlyDictionary<string, string?>> GetManyAsync(IReadOnlyCollection<string> keys) =>
            Task.FromResult<IReadOnlyDictionary<string, string?>>(
                keys.ToDictionary(key => key, key => _values.GetValueOrDefault(key)));

        public Task RemoveAsync(string key)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }

        public Task ExpireAsync(string key, TimeSpan expiration, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ListRightPushTrimAndExpireAsync(string key, string value, int maxLength, TimeSpan expiration, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> ListRangeAsync(string key, long start = 0, long stop = -1, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<long> IncrementAsync(string key, TimeSpan expiration) => Task.FromResult(1L);
        public Task GeoAddAsync(string key, double longitude, double latitude, string member) => Task.CompletedTask;
        public Task GeoRemoveAsync(string key, string member, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> GeoRadiusAsync(string key, double longitude, double latitude, double radiusKm, int count) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<OtpVerificationResult> VerifyAndConsumeOtpAsync(string otpKey, string attemptsKey, string expectedHash, int maxAttempts) => Task.FromResult(OtpVerificationResult.Missing);
        public Task<TripTrackingUpdateResult> RecordTripTrackingPointAsync(TripTrackingPoint point, TripTrackingWriteOptions options, CancellationToken cancellationToken = default) => Task.FromResult(new TripTrackingUpdateResult(true, true, 0, 0, "accepted"));
        public Task<TripTrackingSnapshot> GetTripTrackingSnapshotAsync(long tripId, CancellationToken cancellationToken = default) => Task.FromResult(new TripTrackingSnapshot([], 0, null, null, null, null));
        public Task RemoveTripTrackingAsync(long tripId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RealtimeNotificationServiceFake : IRealtimeNotificationService
    {
        public List<BookingDriverAssignedEvent> DriverAssignments { get; } = [];
        public List<CustomerConfirmedDriverOfferEvent> CustomerConfirmedOffers { get; } = [];
        public List<DriverOfferCancelledEvent> CancelledOffers { get; } = [];

        public Task PublishBookingDriverAssignedAsync(BookingDriverAssignedEvent notification, CancellationToken cancellationToken = default) { DriverAssignments.Add(notification); return Task.CompletedTask; }
        public Task PublishCustomerConfirmedDriverOfferAsync(CustomerConfirmedDriverOfferEvent notification, CancellationToken cancellationToken = default) { CustomerConfirmedOffers.Add(notification); return Task.CompletedTask; }
        public Task PublishDriverOfferCancelledAsync(DriverOfferCancelledEvent notification, CancellationToken cancellationToken = default) { CancelledOffers.Add(notification); return Task.CompletedTask; }
        public Task PublishBookingStatusChangedAsync(BookingStatusChangedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishBookingSearchingStartedAsync(BookingSearchingStartedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishTripCreatedAsync(TripCreatedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishTripStatusChangedAsync(TripStatusChangedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishTripPaymentPendingAsync(TripPaymentPendingEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishTripPaymentSucceededAsync(TripPaymentSucceededEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishSOSTriggeredAsync(SOSTriggeredEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverLocationUpdatedAsync(DriverLocationUpdatedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverOfferCreatedAsync(DriverOfferCreatedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverOfferReceivedAsync(DriverOfferReceivedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverOfferRejectedAsync(DriverOfferRejectedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverOfferAcceptedAsync(DriverOfferAcceptedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverOfferExpiredAsync(DriverOfferExpiredEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverMatchedAsync(DriverMatchedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishBookingSearchRadiusExpandedAsync(BookingSearchRadiusExpandedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishBookingExpiredAsync(BookingExpiredEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class OptionsMonitorFake<TOptions>(TOptions currentValue) : IOptionsMonitor<TOptions>
    {
        public TOptions CurrentValue { get; } = currentValue;
        public TOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<TOptions, string?> listener) => null;
    }
}
