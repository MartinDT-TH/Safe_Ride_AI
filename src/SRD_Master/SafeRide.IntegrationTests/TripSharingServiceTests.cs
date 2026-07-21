using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.TripSharing;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.BackgroundJobs;
using SafeRide.Infrastructure.Redis;
using SafeRide.Infrastructure.Services;

namespace SafeRide.IntegrationTests;

public sealed class TripSharingServiceTests
{
    [Fact]
    public async Task Create_StoresHashAndResolveSetsOpenedAtOnlyOnce()
    {
        await using var fixture = await Fixture.CreateAsync();

        var created = await fixture.Service.CreateAsync(
            fixture.Trip.Id,
            fixture.Owner.Id,
            fixture.Recipient.PhoneNumber!);
        var persisted = await fixture.Db.TripShares.SingleAsync();

        Assert.Equal(64, persisted.TokenHash.Length);
        Assert.DoesNotContain(created.ShareUrl.Split("t=")[1], persisted.TokenHash);

        await fixture.Service.ResolveAsync(
            created.ShareUrl.Split("t=")[1],
            fixture.Recipient.Id);
        var firstOpenedAt = persisted.OpenedAt;
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(2);
        await fixture.Service.ResolveAsync(
            created.ShareUrl.Split("t=")[1],
            fixture.Recipient.Id);

        Assert.NotNull(firstOpenedAt);
        Assert.Equal(firstOpenedAt, persisted.OpenedAt);
    }

    [Fact]
    public async Task Create_DuplicateActiveShareRotatesTokenAndKeepsOneRecord()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.Service.CreateAsync(
            fixture.Trip.Id,
            fixture.Owner.Id,
            fixture.Recipient.PhoneNumber!);
        var second = await fixture.Service.CreateAsync(
            fixture.Trip.Id,
            fixture.Owner.Id,
            fixture.Recipient.PhoneNumber!);

        Assert.Equal(first.TripShareId, second.TripShareId);
        Assert.NotEqual(first.ShareUrl, second.ShareUrl);
        Assert.Single(fixture.Db.TripShares);
    }

    [Fact]
    public async Task Create_AfterExpirationPreservesOldRecordAndCreatesNewShare()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.Service.CreateAsync(
            fixture.Trip.Id,
            fixture.Owner.Id,
            fixture.Recipient.PhoneNumber!);
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddHours(7);

        var second = await fixture.Service.CreateAsync(
            fixture.Trip.Id,
            fixture.Owner.Id,
            fixture.Recipient.PhoneNumber!);

        Assert.NotEqual(first.TripShareId, second.TripShareId);
        var shares = await fixture.Db.TripShares.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, shares.Count);
        Assert.NotNull(shares[0].RevokedAt);
        Assert.Null(shares[1].RevokedAt);
    }

    [Fact]
    public async Task Create_AllowsMultipleRecipientsButRejectsSelfAndTerminalTrip()
    {
        await using var fixture = await Fixture.CreateAsync();
        var secondRecipient = fixture.AddUser("+84901112223", "Người nhận 2");
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.CreateAsync(fixture.Trip.Id, fixture.Owner.Id, fixture.Recipient.PhoneNumber!);
        await fixture.Service.CreateAsync(fixture.Trip.Id, fixture.Owner.Id, secondRecipient.PhoneNumber!);
        Assert.Equal(2, await fixture.Db.TripShares.CountAsync());

        var selfError = await Assert.ThrowsAsync<TripSharingException>(() =>
            fixture.Service.CreateAsync(fixture.Trip.Id, fixture.Owner.Id, fixture.Owner.PhoneNumber!));
        Assert.Equal(400, selfError.StatusCode);

        fixture.Trip.TripStatus = TripStatus.COMPLETED;
        await fixture.Db.SaveChangesAsync();
        var terminalError = await Assert.ThrowsAsync<TripSharingException>(() =>
            fixture.Service.CreateAsync(fixture.Trip.Id, fixture.Owner.Id, "+84909998888"));
        Assert.Equal(409, terminalError.StatusCode);
    }

    [Fact]
    public async Task Create_RejectsNonOwnerMissingLockedAndInactiveRecipient()
    {
        await using var fixture = await Fixture.CreateAsync();

        var nonOwner = await Assert.ThrowsAsync<TripSharingException>(() =>
            fixture.Service.CreateAsync(fixture.Trip.Id, fixture.Recipient.Id, fixture.Owner.PhoneNumber!));
        Assert.Equal(403, nonOwner.StatusCode);

        var missing = await Assert.ThrowsAsync<TripSharingException>(() =>
            fixture.Service.CreateAsync(fixture.Trip.Id, fixture.Owner.Id, "+84900000000"));
        Assert.Equal(404, missing.StatusCode);

        fixture.Recipient.LockoutEnd = new DateTimeOffset(fixture.Clock.UtcNow.AddMinutes(10));
        await fixture.Db.SaveChangesAsync();
        var locked = await Assert.ThrowsAsync<TripSharingException>(() =>
            fixture.Service.CreateAsync(fixture.Trip.Id, fixture.Owner.Id, fixture.Recipient.PhoneNumber!));
        Assert.Equal(409, locked.StatusCode);

        fixture.Recipient.LockoutEnd = null;
        fixture.Recipient.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        var inactive = await Assert.ThrowsAsync<TripSharingException>(() =>
            fixture.Service.CreateAsync(fixture.Trip.Id, fixture.Owner.Id, fixture.Recipient.PhoneNumber!));
        Assert.Equal(409, inactive.StatusCode);
    }

    [Fact]
    public async Task Resolve_RejectsInvalidAndExpiredToken()
    {
        await using var fixture = await Fixture.CreateAsync();
        var invalid = await Assert.ThrowsAsync<TripSharingException>(() =>
            fixture.Service.ResolveAsync("invalid-token", fixture.Recipient.Id));
        Assert.Equal(404, invalid.StatusCode);

        var created = await fixture.Service.CreateAsync(
            fixture.Trip.Id,
            fixture.Owner.Id,
            fixture.Recipient.PhoneNumber!);
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddHours(7);
        var expired = await Assert.ThrowsAsync<TripSharingException>(() =>
            fixture.Service.ResolveAsync(created.ShareUrl.Split("t=")[1], fixture.Recipient.Id));
        Assert.Equal(410, expired.StatusCode);
    }

    [Fact]
    public async Task ListReceived_OnlyReturnsActiveSharesForAnActiveRecipient()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Service.CreateAsync(
            fixture.Trip.Id,
            fixture.Owner.Id,
            fixture.Recipient.PhoneNumber!);

        var received = await fixture.Service.ListReceivedAsync(fixture.Recipient.Id, activeOnly: true);
        var share = Assert.Single(received);
        Assert.Equal(created.TripShareId, share.TripShareId);
        Assert.Equal(TripStatus.IN_PROGRESS.ToString(), share.TripStatus);

        fixture.Recipient.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        Assert.Empty(await fixture.Service.ListReceivedAsync(fixture.Recipient.Id, activeOnly: true));
    }

    [Fact]
    public async Task Tracking_RevalidatesRecipientAndExpiration()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Service.CreateAsync(
            fixture.Trip.Id,
            fixture.Owner.Id,
            fixture.Recipient.PhoneNumber!);

        var tracking = await fixture.Service.GetTrackingAsync(created.TripShareId, fixture.Recipient.Id);
        Assert.Equal(created.TripShareId, tracking.TripShareId);
        Assert.DoesNotContain(fixture.Owner.PhoneNumber!, System.Text.Json.JsonSerializer.Serialize(tracking));

        var wrongRecipient = await Assert.ThrowsAsync<TripSharingException>(() =>
            fixture.Service.GetTrackingAsync(created.TripShareId, fixture.Owner.Id));
        Assert.Equal(403, wrongRecipient.StatusCode);

        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddHours(7);
        var expired = await Assert.ThrowsAsync<TripSharingException>(() =>
            fixture.Service.GetTrackingAsync(created.TripShareId, fixture.Recipient.Id));
        Assert.Equal(410, expired.StatusCode);
    }

    [Fact]
    public async Task ResolveAndTracking_RejectWrongRecipientAndRevokedShare()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Service.CreateAsync(
            fixture.Trip.Id,
            fixture.Owner.Id,
            fixture.Recipient.PhoneNumber!);
        var rawToken = created.ShareUrl.Split("t=")[1];

        var forbidden = await Assert.ThrowsAsync<TripSharingException>(() =>
            fixture.Service.ResolveAsync(rawToken, fixture.Owner.Id));
        Assert.Equal(403, forbidden.StatusCode);

        var revokeForbidden = await Assert.ThrowsAsync<TripSharingException>(() =>
            fixture.Service.RevokeAsync(fixture.Trip.Id, created.TripShareId, fixture.Recipient.Id));
        Assert.Equal(403, revokeForbidden.StatusCode);

        await fixture.Service.RevokeAsync(
            fixture.Trip.Id,
            created.TripShareId,
            fixture.Owner.Id);
        Assert.Single(fixture.Realtime.StatusEvents.Where(x => x.EventName == "TripShareRevoked"));
        await fixture.Service.RevokeAsync(
            fixture.Trip.Id,
            created.TripShareId,
            fixture.Owner.Id);

        var gone = await Assert.ThrowsAsync<TripSharingException>(() =>
            fixture.Service.GetTrackingAsync(created.TripShareId, fixture.Recipient.Id));
        Assert.Equal(410, gone.StatusCode);
    }

    [Theory]
    [InlineData(TripStatus.COMPLETED, 15, "SharedTripCompleted")]
    [InlineData(TripStatus.CANCELLED, 5, "SharedTripCancelled")]
    public async Task TerminalLifecycleShortensExpiryStopsLocationAndSchedulesExpiration(
        TripStatus terminalStatus,
        int graceMinutes,
        string expectedEvent)
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Service.CreateAsync(
            fixture.Trip.Id,
            fixture.Owner.Id,
            fixture.Recipient.PhoneNumber!);
        fixture.Trip.TripStatus = terminalStatus;
        await fixture.Db.SaveChangesAsync();

        await fixture.Service.HandleTripLifecycleAsync(
            fixture.Trip.Id,
            terminalStatus,
            fixture.Clock.UtcNow);
        await fixture.Service.PublishLocationAsync(
            fixture.Trip.Id,
            10.76,
            106.66,
            fixture.Clock.UtcNow);

        var share = await fixture.Db.TripShares.SingleAsync(x => x.Id == created.TripShareId);
        Assert.Equal(fixture.Clock.UtcNow.AddMinutes(graceMinutes), share.ExpiresAt);
        Assert.Contains(fixture.Realtime.StatusEvents, x => x.EventName == expectedEvent);
        Assert.Empty(fixture.Realtime.LocationEvents);
        Assert.Contains(fixture.Scheduler.Scheduled, x => x.TripShareId == share.Id && x.ExpiresAt == share.ExpiresAt);
    }

    [Fact]
    public async Task ExpirationJob_IgnoresStaleScheduleAndPublishesCurrentExpiration()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.Service.CreateAsync(
            fixture.Trip.Id,
            fixture.Owner.Id,
            fixture.Recipient.PhoneNumber!);
        var oldExpiry = (await fixture.Db.TripShares.SingleAsync()).ExpiresAt;

        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(1);
        await fixture.Service.CreateAsync(
            fixture.Trip.Id,
            fixture.Owner.Id,
            fixture.Recipient.PhoneNumber!);
        var currentExpiry = (await fixture.Db.TripShares.SingleAsync()).ExpiresAt;
        var job = new ExpireTripShareJob(
            fixture.Db,
            fixture.Realtime,
            fixture.Clock,
            NullLogger<ExpireTripShareJob>.Instance);

        fixture.Clock.UtcNow = oldExpiry;
        await job.ExecuteAsync(first.TripShareId, oldExpiry.Ticks);
        Assert.DoesNotContain(fixture.Realtime.StatusEvents, x => x.EventName == "TripShareExpired");

        fixture.Clock.UtcNow = currentExpiry;
        await job.ExecuteAsync(first.TripShareId, currentExpiry.Ticks);
        Assert.Contains(fixture.Realtime.StatusEvents, x => x.EventName == "TripShareExpired");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(
            ApplicationDbContext db,
            MutableClock clock,
            TripSharingService service,
            RealtimeFake realtime,
            ExpirySchedulerFake scheduler,
            AspNetUser owner,
            AspNetUser recipient,
            Trip trip)
        {
            Db = db;
            Clock = clock;
            Service = service;
            Realtime = realtime;
            Scheduler = scheduler;
            Owner = owner;
            Recipient = recipient;
            Trip = trip;
        }

        public ApplicationDbContext Db { get; }
        public MutableClock Clock { get; }
        public TripSharingService Service { get; }
        public RealtimeFake Realtime { get; }
        public ExpirySchedulerFake Scheduler { get; }
        public AspNetUser Owner { get; }
        public AspNetUser Recipient { get; }
        public Trip Trip { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"trip-sharing-{Guid.NewGuid():N}")
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var db = new ApplicationDbContext(options);
            var clock = new MutableClock { UtcNow = new DateTime(2026, 7, 15, 9, 0, 0, DateTimeKind.Utc) };

            var owner = AddUser(db, "+84901234567", "Chủ chuyến đi");
            var recipient = AddUser(db, "+84907654321", "Người nhận");
            var driverUser = AddUser(db, "+84908889999", "Tài xế");
            var vehicle = new Vehicle
            {
                Id = 1,
                OwnerUserId = owner.Id,
                OwnerUser = owner,
                PlateNumber = "43A12345",
                BrandModel = "Toyota Vios",
                RequiredLicenseClass = RequiredLicenseClass.B,
                VehicleType = VehicleType.Car,
                EngineType = EngineType.ICE,
                TransmissionType = TransmissionType.Automatic
            };
            var booking = new Booking
            {
                BookingId = 1,
                CustomerId = owner.Id,
                Customer = owner,
                VehicleId = vehicle.Id,
                Vehicle = vehicle,
                ServiceTypeId = 1,
                PickupAddress = "Điểm đón",
                PickupLocation = new Point(106.66, 10.76) { SRID = 4326 },
                DestinationAddress = "Điểm đến",
                DestinationLocation = new Point(106.70, 10.80) { SRID = 4326 },
                EstimatedFare = 100000,
                CreatedAt = clock.UtcNow,
                UpdatedAt = clock.UtcNow
            };
            var driver = new DriverProfile
            {
                DriverId = driverUser.Id,
                Driver = driverUser,
                IdentityCardNumber = "TEST",
                WorkStatus = DriverWorkStatus.Busy
            };
            var trip = new Trip
            {
                Id = 1,
                BookingId = booking.BookingId,
                Booking = booking,
                DriverId = driver.DriverId,
                Driver = driver,
                TripStatus = TripStatus.IN_PROGRESS,
                CreatedAt = clock.UtcNow
            };
            booking.Trip = trip;
            db.AddRange(owner, recipient, driverUser, vehicle, booking, driver, trip);
            await db.SaveChangesAsync();

            var realtime = new RealtimeFake();
            var scheduler = new ExpirySchedulerFake();
            var service = new TripSharingService(
                db,
                new InMemoryRedisService(),
                realtime,
                clock,
                new OptionsMonitorFake<TripSharingOptions>(new TripSharingOptions
                {
                    AppLinkBaseUrl = "https://app.saferide.vn/trip-share",
                    DefaultExpirationHours = 6,
                    CompletedGraceMinutes = 15,
                    CancelledGraceMinutes = 5
                }),
                scheduler);
            return new Fixture(db, clock, service, realtime, scheduler, owner, recipient, trip);
        }

        public AspNetUser AddUser(string phone, string name) => AddUser(Db, phone, name);

        private static AspNetUser AddUser(ApplicationDbContext db, string phone, string name)
        {
            var user = new AspNetUser
            {
                Id = Guid.NewGuid(),
                UserName = phone,
                PhoneNumber = phone,
                PhoneNumberConfirmed = true,
                FullName = name,
                IsActive = true
            };
            db.Users.Add(user);
            return user;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class MutableClock : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; }
    }

    private sealed class ExpirySchedulerFake : ITripShareExpiryScheduler
    {
        public List<(long TripShareId, DateTime ExpiresAt)> Scheduled { get; } = [];

        public void ScheduleExpiration(long tripShareId, DateTime expiresAt)
        {
            Scheduled.Add((tripShareId, expiresAt));
        }
    }

    private sealed class RealtimeFake : IRealtimeNotificationService
    {
        public List<(SharedTripStatusUpdate Update, string EventName)> StatusEvents { get; } = [];
        public List<SharedTripLocationUpdate> LocationEvents { get; } = [];

        public Task PublishSharedTripStatusAsync(SharedTripStatusUpdate notification, string eventName, CancellationToken cancellationToken = default)
        {
            StatusEvents.Add((notification, eventName));
            return Task.CompletedTask;
        }

        public Task PublishSharedTripLocationUpdatedAsync(SharedTripLocationUpdate notification, CancellationToken cancellationToken = default)
        {
            LocationEvents.Add(notification);
            return Task.CompletedTask;
        }

        public Task PublishBookingStatusChangedAsync(SafeRide.Application.Common.Realtime.BookingStatusChangedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishBookingSearchingStartedAsync(SafeRide.Application.Common.Realtime.BookingSearchingStartedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishTripCreatedAsync(SafeRide.Application.Common.Realtime.TripCreatedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishBookingDriverAssignedAsync(SafeRide.Application.Common.Realtime.BookingDriverAssignedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishTripStatusChangedAsync(SafeRide.Application.Common.Realtime.TripStatusChangedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishTripPaymentPendingAsync(SafeRide.Application.Common.Realtime.TripPaymentPendingEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishTripPaymentSucceededAsync(SafeRide.Application.Common.Realtime.TripPaymentSucceededEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverLocationUpdatedAsync(SafeRide.Application.Common.Realtime.DriverLocationUpdatedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverOfferCreatedAsync(SafeRide.Application.Common.Realtime.DriverOfferCreatedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverOfferReceivedAsync(SafeRide.Application.Common.Realtime.DriverOfferReceivedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverOfferRejectedAsync(SafeRide.Application.Common.Realtime.DriverOfferRejectedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverOfferAcceptedAsync(SafeRide.Application.Common.Realtime.DriverOfferAcceptedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverOfferExpiredAsync(SafeRide.Application.Common.Realtime.DriverOfferExpiredEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverOfferCancelledAsync(SafeRide.Application.Common.Realtime.DriverOfferCancelledEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishDriverMatchedAsync(SafeRide.Application.Common.Realtime.DriverMatchedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishCustomerConfirmedDriverOfferAsync(SafeRide.Application.Common.Realtime.CustomerConfirmedDriverOfferEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishBookingSearchRadiusExpandedAsync(SafeRide.Application.Common.Realtime.BookingSearchRadiusExpandedEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishBookingExpiredAsync(SafeRide.Application.Common.Realtime.BookingExpiredEvent notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class OptionsMonitorFake<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
