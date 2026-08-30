using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Application.Features.Bookings;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Services;

namespace SafeRide.IntegrationTests;

public sealed class CustomerBookingPrivilegeServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CustomerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task Recalculate_OneVerifiedNoShow_IsReminderWithoutRestriction()
    {
        await using var context = CreateContext();
        AddNoShows(context, 1);
        await context.SaveChangesAsync();

        var privilege = await CreateService(context).RecalculateAsync(CustomerId, CancellationToken.None);

        Assert.Equal(CustomerBehaviorRestrictionLevel.REMINDER, privilege.RestrictionLevel);
        Assert.Equal(1, privilege.VerifiedNoShowCount);
        Assert.True(privilege.ScheduledBookingAllowed);
        Assert.True(privilege.InstantBookingAllowed);
    }

    [Fact]
    public async Task Recalculate_ThreeNoShowsWithEligibleBookings_RestrictsScheduleOnly()
    {
        await using var context = CreateContext();
        AddNoShows(context, 3);
        AddCompletedTrips(context, 5);
        await context.SaveChangesAsync();

        var privilege = await CreateService(context).RecalculateAsync(CustomerId, CancellationToken.None);

        Assert.Equal(CustomerBehaviorRestrictionLevel.SCHEDULE_RISK, privilege.RestrictionLevel);
        Assert.Equal(3m / 8m, privilege.NoShowRate);
        Assert.False(privilege.ScheduledBookingAllowed);
        Assert.True(privilege.InstantBookingAllowed);
        Assert.NotNull(privilege.ScheduledRestrictedUntil);
    }

    [Fact]
    public async Task Recalculate_FourNoShowsAtFortyPercent_EnablesPersistentCooldown()
    {
        await using var context = CreateContext();
        AddNoShows(context, 4);
        AddCompletedTrips(context, 4);
        await context.SaveChangesAsync();

        var privilege = await CreateService(context).RecalculateAsync(CustomerId, CancellationToken.None);

        Assert.Equal(CustomerBehaviorRestrictionLevel.PERSISTENT_ABUSE, privilege.RestrictionLevel);
        Assert.False(privilege.InstantBookingAllowed);
        Assert.NotNull(privilege.BookingCooldownUntil);
        Assert.Equal(14, (privilege.ScheduledRestrictedUntil!.Value - Now).Days);
    }

    [Fact]
    public async Task Recalculate_ExemptNoShowIsExcluded_AndCompletedTripBreaksStreak()
    {
        await using var context = CreateContext();
        AddNoShows(context, 2);
        context.CustomerBehaviorEvents.Add(new CustomerBehaviorEvent
        {
            CustomerId = CustomerId,
            BookingId = 100,
            TripId = 100,
            EventType = CustomerBehaviorEventType.EXEMPTED_NO_SHOW,
            Status = CustomerBehaviorEventStatus.EXEMPTED,
            VerifiedAt = Now.AddDays(-1),
            CreatedAt = Now.AddDays(-1),
            UpdatedAt = Now.AddDays(-1)
        });
        AddCompletedTrips(context, 1, Now.AddDays(-1));
        await context.SaveChangesAsync();

        var privilege = await CreateService(context).RecalculateAsync(CustomerId, CancellationToken.None);

        Assert.Equal(2, privilege.VerifiedNoShowCount);
        Assert.Equal(0, privilege.ConsecutiveNoShowStreak);
        Assert.Equal(CustomerBehaviorRestrictionLevel.WARNING, privilege.RestrictionLevel);
    }

    [Fact]
    public async Task EnsureCanCreate_RejectsOnlyTheRestrictedBookingType()
    {
        await using var context = CreateContext();
        context.CustomerBookingPrivileges.Add(new CustomerBookingPrivilege
        {
            CustomerId = CustomerId,
            ScheduledBookingAllowed = false,
            ScheduledRestrictedUntil = Now.AddDays(7),
            InstantBookingAllowed = true
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await Assert.ThrowsAsync<BookingException>(() => service.EnsureCanCreateAsync(
            CustomerId, BookingType.Scheduled, Now, CancellationToken.None));
        await service.EnsureCanCreateAsync(CustomerId, BookingType.Now, Now, CancellationToken.None);
    }

    private static CustomerBookingPrivilegeService CreateService(ApplicationDbContext context) =>
        new(context, new TestClock(Now), new TestOptionsMonitor<CustomerNoShowOptions>(new()));

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"privilege-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void AddNoShows(ApplicationDbContext context, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var timestamp = Now.AddDays(-count + i);
            context.CustomerBehaviorEvents.Add(new CustomerBehaviorEvent
            {
                CustomerId = CustomerId,
                BookingId = i + 1,
                TripId = i + 1,
                EventType = CustomerBehaviorEventType.VERIFIED_NO_SHOW,
                Status = CustomerBehaviorEventStatus.VERIFIED,
                VerifiedAt = timestamp,
                CreatedAt = timestamp,
                UpdatedAt = timestamp
            });
        }
    }

    private static void AddCompletedTrips(ApplicationDbContext context, int count, DateTime? completedAt = null)
    {
        for (var i = 0; i < count; i++)
        {
            context.Trips.Add(new Trip
            {
                Booking = new Booking
                {
                    CustomerId = CustomerId,
                    PickupAddress = "Pickup",
                    PickupLocation = new Point(106.66, 10.76) { SRID = 4326 }
                },
                DriverId = Guid.NewGuid(),
                TripStatus = TripStatus.COMPLETED,
                CompletedAt = completedAt ?? Now.AddDays(-1),
                CreatedAt = Now.AddDays(-2)
            });
        }
    }

    private sealed class TestClock(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow => utcNow;
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
