using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Common.Realtime;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.Bookings.Commands.CreateBooking;
using SafeRide.Application.Features.Bookings.Queries.EstimateBookingFare;
using SafeRide.Application.Features.Bookings.Services;
using SafeRide.Application.Features.Promotions;
using SafeRide.Application.Features.Vehicles.Services;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.UnitTests;

public sealed class BookingTests
{
    private static readonly DateTime UtcNow =
        new(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CalculateFare_PerTrip_UsesDistance()
    {
        var service = new FareEstimationService();
        var rule = CreatePricingRule(pricePerKm: 10_000m);

        var fare = service.CalculateFare(rule, 5.2m, 30);

        Assert.Equal(72_000m, fare);
    }

    [Fact]
    public void CalculateFare_PerHour_UsesEstimatedDuration()
    {
        var service = new FareEstimationService();
        var rule = CreatePricingRule(pricePerHour: 60_000m);

        var fare = service.CalculateFare(rule, 5.2m, 90);

        Assert.Equal(110_000m, fare);
    }

    [Fact]
    public async Task Handle_NowBooking_SavesAndStartsMatching()
    {
        var fixture = new HandlerFixture();
        var command = fixture.CreateCommand(BookingType.Now, null);

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.Equal(BookingStatus.Searching, result.BookingStatus);
        Assert.Equal(42, result.BookingId);
        Assert.Equal(1, fixture.UnitOfWork.SaveCount);
        Assert.Equal([42L], fixture.MatchingService.BookingIds);
        Assert.Equal("polyline", fixture.Repository.AddedBooking?.RoutePolyline);
    }

    [Fact]
    public async Task EstimateFare_ReturnsRouteAndCalculatedFare()
    {
        var fixture = new HandlerFixture();
        var handler = new EstimateBookingFareQueryHandler(
            fixture.Repository,
            new MapServiceFake(),
            new FareEstimationService(),
            new VehicleLicenseRequirementService(),
            new DateTimeProviderFake(UtcNow));

        var result = await handler.Handle(
            new EstimateBookingFareQuery(
                HandlerFixture.CustomerId,
                1,
                2,
                10.762622,
                106.660172,
                10.818797,
                106.651856,
                null),
            CancellationToken.None);

        Assert.Equal(5.2, result.EstimatedDistanceKm);
        Assert.Equal(30, result.EstimatedDurationMinutes);
        Assert.Equal("polyline", result.EncodedPolyline);
        Assert.Equal(72_000m, result.EstimatedFare);
    }

    [Fact]
    public async Task Handle_ScheduledBooking_DoesNotStartMatchingImmediately()
    {
        var fixture = new HandlerFixture();
        var scheduledAt = UtcNow.AddMinutes(30);
        var command = fixture.CreateCommand(BookingType.Scheduled, scheduledAt);

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.Equal(BookingStatus.PendingSchedule, result.BookingStatus);
        Assert.Equal(scheduledAt, result.ScheduledAt);
        Assert.Empty(fixture.MatchingService.BookingIds);
    }

    [Fact]
    public async Task Handle_NowBookingWithActiveNowBooking_Throws()
    {
        var fixture = new HandlerFixture();
        fixture.Repository.ActiveNowBooking = new Booking
        {
            BookingId = 10,
            CustomerId = HandlerFixture.CustomerId,
            BookingType = BookingType.Now,
            BookingStatus = BookingStatus.Searching
        };
        var command = fixture.CreateCommand(BookingType.Now, null);

        var exception = await Assert.ThrowsAsync<BookingException>(
            () => fixture.Handler.Handle(command, CancellationToken.None));

        Assert.Equal("booking.active_now_exists", exception.Code);
        Assert.Null(fixture.Repository.AddedBooking);
        Assert.Empty(fixture.MatchingService.BookingIds);
    }

    [Fact]
    public async Task Handle_ScheduledBookingWithActiveNowBooking_IsAllowed()
    {
        var fixture = new HandlerFixture();
        fixture.Repository.ActiveNowBooking = new Booking
        {
            BookingId = 10,
            CustomerId = HandlerFixture.CustomerId,
            BookingType = BookingType.Now,
            BookingStatus = BookingStatus.DriverAssigned
        };
        var command = fixture.CreateCommand(BookingType.Scheduled, UtcNow.AddMinutes(30));

        var result = await fixture.Handler.Handle(command, CancellationToken.None);

        Assert.Equal(BookingStatus.PendingSchedule, result.BookingStatus);
        Assert.NotNull(fixture.Repository.AddedBooking);
    }

    [Fact]
    public async Task Handle_ScheduledBookingLessThanThirtyMinutes_Throws()
    {
        var fixture = new HandlerFixture();
        var command = fixture.CreateCommand(
            BookingType.Scheduled,
            UtcNow.AddMinutes(29));

        var exception = await Assert.ThrowsAsync<BookingException>(
            () => fixture.Handler.Handle(command, CancellationToken.None));

        Assert.Equal("booking.invalid_schedule", exception.Code);
    }

    private static PricingRule CreatePricingRule(
        decimal? pricePerKm = null,
        decimal? pricePerHour = null)
    {
        return new PricingRule
        {
            Id = 7,
            BaseFare = 20_000m,
            MinFare = 30_000m,
            PricePerKm = pricePerKm,
            PricePerHour = pricePerHour
        };
    }

    private sealed class HandlerFixture
    {
        public HandlerFixture()
        {
            Repository = new BookingRepositoryFake
            {
                Vehicle = new Vehicle
                {
                    Id = 1,
                    OwnerUserId = CustomerId,
                    VehicleType = VehicleType.Motorbike,
                    EngineCapacityCc = 110,
                    RequiredLicenseClass = RequiredLicenseClass.A1
                },
                PricingRule = CreatePricingRule(pricePerKm: 10_000m)
            };
            UnitOfWork = new UnitOfWorkFake();
            MatchingService = new MatchingServiceFake();
            Handler = new CreateBookingCommandHandler(
                Repository,
                UnitOfWork,
                new DateTimeProviderFake(UtcNow),
                new MapServiceFake(),
                new FareEstimationService(),
                MatchingService,
                new VehicleLicenseRequirementService(),
                new RealtimeNotificationServiceFake(),
                Repository,
                new MatchingPolicyProviderFake(),
                new BookingLifecycleJobSchedulerFake());
        }

        public static readonly Guid CustomerId =
            Guid.Parse("11111111-1111-1111-1111-111111111111");

        public BookingRepositoryFake Repository { get; }
        public UnitOfWorkFake UnitOfWork { get; }
        public MatchingServiceFake MatchingService { get; }
        public CreateBookingCommandHandler Handler { get; }

        public CreateBookingCommand CreateCommand(
            BookingType bookingType,
            DateTime? scheduledAt)
        {
            return new CreateBookingCommand(
                CustomerId,
                1,
                2,
                bookingType,
                scheduledAt,
                "Điểm đón",
                10.762622,
                106.660172,
                "Điểm đến",
                10.818797,
                106.651856,
                null,
                null,
                null);
        }
    }

    private sealed class BookingRepositoryFake : IBookingRepository, IPromotionRepository
    {
        public Vehicle? Vehicle { get; init; }
        public PricingRule? PricingRule { get; init; }
        public Promotion? Promotion { get; init; }
        public Booking? ActiveNowBooking { get; set; }
        public Booking? AddedBooking { get; private set; }

        public Task AddAsync(Booking booking, CancellationToken cancellationToken)
        {
            booking.BookingId = 42;
            AddedBooking = booking;
            return Task.CompletedTask;
        }

        public Task<Booking?> GetCustomerBookingAsync(
            long bookingId,
            Guid customerId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<Booking?>(AddedBooking);
        }

        public Task<Booking?> GetCustomerBookingWithDetailsAsync(
            long bookingId,
            Guid customerId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<Booking?>(AddedBooking);
        }

        public Task<Booking?> GetActiveNowBookingAsync(
            Guid customerId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ActiveNowBooking);
        }

        public Task<Application.Features.Bookings.DTOs.BookingDriverOfferDto?> GetLatestBookingDriverOfferAsync(
            long bookingId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<Application.Features.Bookings.DTOs.BookingDriverOfferDto?>(null);
        }

        public Task<LocationPoint?> GetDriverLocationAsync(
            Guid driverId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<LocationPoint?>(null);
        }

        public Task ExpireStaleNowBookingsAsync(
            Guid customerId,
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<Vehicle?> GetCustomerVehicleAsync(
            long vehicleId,
            Guid customerId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Vehicle);
        }

        public Task<IReadOnlyList<Vehicle>> GetCustomerVehiclesAsync(
            Guid customerId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<Vehicle> vehicles = Vehicle is null
                ? []
                : [Vehicle];

            return Task.FromResult(vehicles);
        }

        public Task<IReadOnlyList<PricingRule>> GetBookablePricingRulesAsync(
            Guid customerId,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<PricingRule> pricingRules = PricingRule is null
                ? []
                : [PricingRule];

            return Task.FromResult(pricingRules);
        }

        public Task<PricingRule?> GetPricingRuleAsync(
            long serviceTypeId,
            long vehicleId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(PricingRule);
        }

        public Task<SurgePricingRule?> GetActiveSurgePricingRuleAsync(
            DateTime currentUtcTime,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<SurgePricingRule?>(null);
        }

        public Task<IReadOnlyList<Booking>> GetScheduledBookingsReadyForMatchingAsync(
            DateTime matchingCutoffUtc,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Booking>>([]);
        }

        public Task CancelActiveDriverOffersAsync(
            long bookingId,
            DateTime cancelledAt,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<bool> CancelAssignedTripAsync(
            long bookingId,
            Guid cancelledByUserId,
            string? reason,
            DateTime cancelledAt,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<Promotion>> GetAvailablePromotionsAsync(
            DateTime utcNow,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<Promotion> promotions = Promotion is null
                ? []
                : [Promotion];

            return Task.FromResult(promotions);
        }

        public Task<Promotion?> GetPromotionByCodeAsync(
            string promotionCode,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Promotion);
        }

        public Task<Booking?> GetBookingForPromotionAsync(
            long bookingId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<Booking?>(AddedBooking);
        }

        public Task<int> CountCustomerPromotionUsageAsync(
            Guid customerId,
            long promotionId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(0);
        }

        public Task AddBookingPromotionAsync(
            BookingPromotion bookingPromotion,
            CancellationToken cancellationToken)
        {
            AddedBooking?.BookingPromotions.Add(bookingPromotion);
            return Task.CompletedTask;
        }

        public Task RemoveBookingPromotionsForBookingAsync(
            long bookingId,
            CancellationToken cancellationToken)
        {
            AddedBooking?.BookingPromotions.Clear();
            return Task.CompletedTask;
        }
    }

    private sealed class UnitOfWorkFake : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.FromResult(1);
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

    private sealed class MapServiceFake : IMapRoutingService
    {
        public Task<RouteEstimateResult> GetRouteEstimateAsync(
            RouteEstimateRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new RouteEstimateResult
            {
                Provider = MapProvider.VietMap,
                DistanceMeters = 5200,   // 5.2 km
                DurationSeconds = 1800,  // 30 min
                EncodedPolyline = "polyline"
            });
        }
    }

    private sealed class MatchingServiceFake : IBookingMatchingService
    {
        public List<long> BookingIds { get; } = [];

        public Task<Application.Features.Bookings.DTOs.BookingDriverOfferDto?> StartMatchingAsync(
            long bookingId,
            CancellationToken cancellationToken)
        {
            BookingIds.Add(bookingId);
            return Task.FromResult<Application.Features.Bookings.DTOs.BookingDriverOfferDto?>(null);
        }
    }

    private sealed class MatchingPolicyProviderFake : IMatchingPolicyProvider
    {
        public MatchingOptions Current { get; } = new();

        public DateTime? GetMatchingStartedAt(Booking booking)
        {
            return booking.BookingType == BookingType.Now
                ? booking.CreatedAt
                : booking.UpdatedAt;
        }

        public BookingMatchingSnapshot GetSnapshot(Booking booking, DateTime utcNow)
        {
            var startedAt = GetMatchingStartedAt(booking) ?? utcNow;
            var expiresAt = startedAt.AddMinutes(Current.BookingExpireAfterMinutes);
            return new BookingMatchingSnapshot(
                Current.InitialRadiusKm,
                expiresAt,
                Math.Max(0, (int)Math.Ceiling((expiresAt - utcNow).TotalSeconds)),
                "SafeRide đang tìm tài xế gần bạn trong bán kính 5km.",
                false);
        }
    }

    private sealed class RealtimeNotificationServiceFake
        : IRealtimeNotificationService
    {
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

        public Task PublishBookingDriverAssignedAsync(
            BookingDriverAssignedEvent notification,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishTripStatusChangedAsync(
            TripStatusChangedEvent notification,
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

    private sealed class BookingLifecycleJobSchedulerFake : IBookingLifecycleJobScheduler
    {
        public void ScheduleExpandRadius(long bookingId, TimeSpan delay) { }
        public void ScheduleExpireBooking(long bookingId, TimeSpan delay) { }
        public void ScheduleExpireDriverOffer(long offerId, TimeSpan delay) { }
        public Task CancelExpireDriverOfferAsync(long offerId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
        public Task CancelJobsForBookingAsync(long bookingId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
