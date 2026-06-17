using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.Bookings.Commands.CreateBooking;
using SafeRide.Application.Features.Bookings.Queries.EstimateBookingFare;
using SafeRide.Application.Features.Bookings.Services;
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
            new FareEstimationService());

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
                Vehicle = new Vehicle { Id = 1, OwnerUserId = CustomerId },
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
                MatchingService);
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
                null);
        }
    }

    private sealed class BookingRepositoryFake : IBookingRepository
    {
        public Vehicle? Vehicle { get; init; }
        public PricingRule? PricingRule { get; init; }
        public Booking? AddedBooking { get; private set; }

        public Task AddAsync(Booking booking, CancellationToken cancellationToken)
        {
            booking.BookingId = 42;
            AddedBooking = booking;
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

        public Task<IReadOnlyList<Booking>> GetScheduledBookingsReadyForMatchingAsync(
            DateTime matchingCutoffUtc,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Booking>>([]);
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

    private sealed class MapServiceFake : IGoogleMapsService
    {
        public Task<RouteEstimateResult> GetRouteEstimateAsync(
            LocationPoint pickup,
            LocationPoint destination,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new RouteEstimateResult(5.2, 30, "polyline"));
        }
    }

    private sealed class MatchingServiceFake : IBookingMatchingService
    {
        public List<long> BookingIds { get; } = [];

        public Task StartMatchingAsync(
            long bookingId,
            CancellationToken cancellationToken)
        {
            BookingIds.Add(bookingId);
            return Task.CompletedTask;
        }
    }
}
