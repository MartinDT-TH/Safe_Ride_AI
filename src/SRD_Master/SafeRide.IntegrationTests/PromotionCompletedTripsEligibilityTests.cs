using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Promotions;
using SafeRide.Application.Features.Promotions.Commands.ApplyPromotionToBooking;
using SafeRide.Application.Features.Promotions.Queries.GetAvailablePromotions;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Repositories;

namespace SafeRide.IntegrationTests;

[Trait(SqlServerTestDatabase.ProviderTraitName, SqlServerTestDatabase.InMemoryProvider)]
public sealed class PromotionCompletedTripsEligibilityTests
{
    private static readonly DateTime UtcNow =
        new(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AvailablePromotions_InsufficientCompletedTrips_ReturnsLockedPromotion()
    {
        await using var fixture = CreateFixture(requiredCompletedTrips: 3);

        var result = await fixture.AvailableHandler.Handle(
            new GetAvailablePromotionsQuery(fixture.CustomerId),
            CancellationToken.None);

        var promotion = Assert.Single(result);
        Assert.False(promotion.IsUnlocked);
        Assert.Equal(3, promotion.RequiredCompletedTrips);
        Assert.Equal(0, promotion.CustomerCompletedTrips);
        Assert.Equal(3, promotion.RemainingTripsToUnlock);
        Assert.Equal(
            "Bạn cần hoàn thành thêm 3 chuyến để sử dụng mã khuyến mãi này.",
            promotion.UnlockMessage);
    }

    [Fact]
    public async Task ApplyPromotion_InsufficientCompletedTrips_ReturnsRemainingTrips()
    {
        await using var fixture = CreateFixture(requiredCompletedTrips: 3);
        await fixture.AddCompletedTripAsync();

        var exception = await Assert.ThrowsAsync<PromotionException>(() =>
            fixture.ApplyHandler.Handle(
                new ApplyPromotionToBookingCommand(
                    fixture.CustomerId,
                    fixture.Booking.BookingId,
                    fixture.Promotion.PromotionCode),
                CancellationToken.None));

        Assert.Equal("promotion.required_completed_trips_not_met", exception.Code);
        Assert.Equal(
            "Bạn cần hoàn thành thêm 2 chuyến để sử dụng mã khuyến mãi này.",
            exception.Message);
    }

    private static Fixture CreateFixture(int requiredCompletedTrips)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"promotion-eligibility-{Guid.NewGuid():N}")
            .Options;
        var dbContext = new ApplicationDbContext(options);
        var repository = new PromotionRepository(dbContext);
        var ruleStore = new PromotionUnlockRuleStoreFake(requiredCompletedTrips);
        var promotion = new Promotion
        {
            PromotionCode = "VIP3",
            DiscountType = DiscountType.Percentage,
            DiscountValue = 20m,
            StartDate = UtcNow.AddDays(-1),
            EndDate = UtcNow.AddDays(10),
            MaxUsageCount = 100,
            MinimumOrderValue = 0,
            MaximumDiscountValue = 30_000m,
            UsageLimitPerUser = 1,
            IsActive = true
        };
        var booking = CreateBooking(Guid.NewGuid());
        dbContext.Promotions.Add(promotion);
        dbContext.Bookings.Add(booking);
        dbContext.SaveChanges();

        return new Fixture(
            dbContext,
            repository,
            ruleStore,
            promotion,
            booking);
    }

    private static Booking CreateBooking(Guid customerId) => new()
    {
        CustomerId = customerId,
        VehicleId = 1,
        ServiceTypeId = 1,
        BookingStatus = BookingStatus.Searching,
        PickupAddress = "Pickup",
        PickupLocation = new Point(108.2, 16.05) { SRID = 4326 },
        EstimatedFare = 100_000m,
        CreatedAt = UtcNow,
        UpdatedAt = UtcNow
    };

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly PromotionRepository _repository;
        private readonly PromotionUnlockRuleStoreFake _ruleStore;

        public Fixture(
            ApplicationDbContext dbContext,
            PromotionRepository repository,
            PromotionUnlockRuleStoreFake ruleStore,
            Promotion promotion,
            Booking booking)
        {
            DbContext = dbContext;
            _repository = repository;
            _ruleStore = ruleStore;
            Promotion = promotion;
            Booking = booking;
            AvailableHandler = new GetAvailablePromotionsQueryHandler(
                repository,
                new DateTimeProviderFake(UtcNow),
                ruleStore);
            ApplyHandler = new ApplyPromotionToBookingCommandHandler(
                repository,
                new UnitOfWork(dbContext),
                new DateTimeProviderFake(UtcNow),
                ruleStore);
        }

        public ApplicationDbContext DbContext { get; }
        public Promotion Promotion { get; }
        public Booking Booking { get; }
        public Guid CustomerId => Booking.CustomerId;
        public GetAvailablePromotionsQueryHandler AvailableHandler { get; }
        public ApplyPromotionToBookingCommandHandler ApplyHandler { get; }

        public async Task AddCompletedTripAsync()
        {
            var completedBooking = CreateBooking(CustomerId);
            completedBooking.BookingStatus = BookingStatus.Completed;
            DbContext.Bookings.Add(completedBooking);
            await DbContext.SaveChangesAsync();
            DbContext.Trips.Add(new Trip
            {
                BookingId = completedBooking.BookingId,
                DriverId = Guid.NewGuid(),
                TripStatus = TripStatus.COMPLETED,
                CompletedAt = UtcNow.AddHours(-1),
                CreatedAt = UtcNow.AddHours(-2)
            });
            await DbContext.SaveChangesAsync();
        }

        public ValueTask DisposeAsync() => DbContext.DisposeAsync();
    }

    private sealed class PromotionUnlockRuleStoreFake(int requiredCompletedTrips)
        : IPromotionUnlockRuleStore
    {
        public Task<int> GetRequiredCompletedTripsAsync(
            string promotionCode,
            CancellationToken cancellationToken) =>
            Task.FromResult(requiredCompletedTrips);

        public Task<IReadOnlyDictionary<string, int>> GetRequiredCompletedTripsAsync(
            IReadOnlyCollection<string> promotionCodes,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, int>>(
                promotionCodes.ToDictionary(code => code, _ => requiredCompletedTrips));

        public Task SaveAsync(
            string promotionCode,
            int value,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RemoveAsync(
            string promotionCode,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class DateTimeProviderFake(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
