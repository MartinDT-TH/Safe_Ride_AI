using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Common.Models;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace SafeRide.IntegrationTests;

public sealed class StaffNoShowReviewServiceTests
{
    private static readonly DateTime Now = new(2026, 8, 29, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CustomerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid DriverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid StaffId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task StaffReview_ExemptMarksEventAndRecalculatesPrivilegeWithoutFinancialSideEffects()
    {
        await using var context = CreateContext();
        SeedGraph(context);
        context.CustomerBehaviorEvents.Add(Event(1));
        context.CustomerBehaviorEvents.Add(Event(2));
        context.DriverNoShowSupports.Add(new DriverNoShowSupport { Id = 1, TripId = 1, BookingId = 1, DriverId = DriverId, CustomerBehaviorEventId = 1, SupportAmount = 10000, AcceptedPickupDistanceKm = 6 });
        context.CustomerBookingPrivileges.Add(new CustomerBookingPrivilege { CustomerId = CustomerId, ScheduledBookingAllowed = false, ScheduledRestrictedUntil = Now.AddDays(7), VerifiedNoShowCount = 2 });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.ExemptAsync(1, StaffId, "GPS mismatch", CancellationToken.None);

        Assert.Equal(CustomerBehaviorEventType.EXEMPTED_NO_SHOW, result.Event.EventType);
        Assert.Equal(CustomerBehaviorEventStatus.EXEMPTED, result.Event.Status);
        Assert.Equal(StaffId, result.Event.ReviewedByUserId);
        Assert.Equal("GPS mismatch", result.Event.ReviewReason);
        Assert.Equal(1, result.Privilege!.VerifiedNoShowCount);
        Assert.Single(await context.DriverNoShowSupports.ToListAsync());
        Assert.Equal(0, await context.WalletTransactions.CountAsync());
    }

    [Fact]
    public async Task StaffReview_ClearRestrictionAllowsBothBookingTypesAndKeepsHistory()
    {
        await using var context = CreateContext();
        SeedGraph(context);
        context.CustomerBehaviorEvents.Add(Event(1));
        context.CustomerBookingPrivileges.Add(new CustomerBookingPrivilege
        {
            CustomerId = CustomerId, ScheduledBookingAllowed = false, ScheduledRestrictedUntil = Now.AddDays(7),
            InstantBookingAllowed = false, BookingCooldownUntil = Now.AddHours(2), UnderStaffReview = true
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var privilege = await service.ClearRestrictionsAsync(CustomerId, StaffId, "Manual recovery", CancellationToken.None);
        var stored = await context.CustomerBookingPrivileges.SingleAsync();

        Assert.True(privilege.ScheduledBookingAllowed);
        Assert.True(privilege.InstantBookingAllowed);
        Assert.Null(privilege.ScheduledRestrictedUntil);
        Assert.Null(privilege.BookingCooldownUntil);
        Assert.False(privilege.UnderStaffReview);
        Assert.Single(await context.CustomerBehaviorEvents.ToListAsync());
        Assert.Equal(stored.CustomerId, CustomerId);
    }

    [Fact]
    public async Task StaffReview_ExemptIsIdempotent()
    {
        await using var context = CreateContext();
        SeedGraph(context);
        context.CustomerBehaviorEvents.Add(Event(1));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await service.ExemptAsync(1, StaffId, "First review", CancellationToken.None);
        var second = await service.ExemptAsync(1, StaffId, "Second review", CancellationToken.None);

        Assert.Equal(CustomerBehaviorEventStatus.EXEMPTED, second.Event.Status);
        Assert.Equal(1, await context.CustomerBehaviorEvents.CountAsync());
    }

    private static StaffNoShowReviewService CreateService(ApplicationDbContext context) => new(context, new CustomerBookingPrivilegeService(context, new Clock(Now), new OptionsMonitor<CustomerNoShowOptions>(new())));

    private static CustomerBehaviorEvent Event(long id) => new() { Id = id, CustomerId = CustomerId, BookingId = id, TripId = id, DriverId = DriverId, EventType = CustomerBehaviorEventType.VERIFIED_NO_SHOW, Status = CustomerBehaviorEventStatus.VERIFIED, VerifiedAt = Now.AddDays(-id), CreatedAt = Now.AddDays(-id), UpdatedAt = Now };

    private static void SeedGraph(ApplicationDbContext context)
    {
        context.Users.Add(new AspNetUser { Id = CustomerId, FullName = "Customer", IsActive = true });
        context.Users.Add(new AspNetUser { Id = DriverId, FullName = "Driver", IsActive = true });
        context.Users.Add(new AspNetUser { Id = StaffId, FullName = "Staff", IsActive = true });
        context.DriverProfiles.Add(new DriverProfile { DriverId = DriverId, Driver = context.Users.Local.Single(x => x.Id == DriverId), IdentityCardNumber = "ID" });
        context.Bookings.Add(new Booking { BookingId = 1, CustomerId = CustomerId, PickupAddress = "Pickup", PickupLocation = new Point(106.66, 10.76) { SRID = 4326 } });
        context.Bookings.Add(new Booking { BookingId = 2, CustomerId = CustomerId, PickupAddress = "Pickup", PickupLocation = new Point(106.66, 10.76) { SRID = 4326 } });
        context.Trips.Add(new Trip { Id = 1, BookingId = 1, DriverId = DriverId });
        context.Trips.Add(new Trip { Id = 2, BookingId = 2, DriverId = DriverId });
    }

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase($"staff-review-{Guid.NewGuid():N}").Options);
    private sealed class Clock(DateTime value) : IDateTimeProvider { public DateTime UtcNow => value; }
    private sealed class OptionsMonitor<T>(T value) : IOptionsMonitor<T> { public T CurrentValue => value; public T Get(string? name) => value; public IDisposable? OnChange(Action<T, string?> listener) => null; }
}
