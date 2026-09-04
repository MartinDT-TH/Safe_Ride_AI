using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using SafeRide.Application.Features.TripSharing;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Redis;
using SafeRide.Infrastructure.Services;

namespace SafeRide.IntegrationTests;

[Trait(SqlServerTestDatabase.ProviderTraitName, SqlServerTestDatabase.SqlServerProvider)]
public sealed class TripSharingSqlServerTests
{
    [SqlServerFact]
    public async Task Create_WithRetryingExecutionStrategy_CommitsShareAndNotification()
    {
        await using var database = await SqlServerTestDatabase.CreateCurrentModelAsync(
            "TripSharingTransaction");
        await using var db = database.CreateDbContext();
        var now = new DateTime(2026, 9, 3, 10, 0, 0, DateTimeKind.Utc);

        var owner = CreateUser("+84901234567", "Trip owner", now);
        var recipient = CreateUser("+84907654321", "Share recipient", now);
        var driverUser = CreateUser("+84908889999", "Trip driver", now);
        var serviceType = new ServiceType { ServiceName = "Trip sharing test" };
        var vehicle = new Vehicle
        {
            OwnerUserId = owner.Id,
            OwnerUser = owner,
            PlateNumber = "43A12345",
            BrandModel = "Test car",
            RequiredLicenseClass = RequiredLicenseClass.B,
            VehicleType = VehicleType.Car,
            EngineType = EngineType.ICE,
            TransmissionType = TransmissionType.Automatic,
            CreatedAt = now
        };
        var booking = new Booking
        {
            CustomerId = owner.Id,
            Customer = owner,
            Vehicle = vehicle,
            ServiceType = serviceType,
            BookingType = BookingType.Now,
            BookingStatus = BookingStatus.DriverAssigned,
            PickupAddress = "Test pickup",
            PickupLocation = new Point(106.66, 10.76) { SRID = 4326 },
            DestinationAddress = "Test destination",
            DestinationLocation = new Point(106.70, 10.80) { SRID = 4326 },
            EstimatedFare = 100_000m,
            CreatedAt = now,
            UpdatedAt = now
        };
        var driver = new DriverProfile
        {
            DriverId = driverUser.Id,
            Driver = driverUser,
            IdentityCardNumber = "SHARE-TEST",
            WorkStatus = DriverWorkStatus.Busy,
            CreatedAt = now
        };
        var trip = new Trip
        {
            Booking = booking,
            DriverId = driver.DriverId,
            Driver = driver,
            TripStatus = TripStatus.IN_PROGRESS,
            StartedAt = now,
            CreatedAt = now
        };
        booking.Trip = trip;
        db.AddRange(owner, recipient, driverUser, serviceType, vehicle, booking, driver, trip);
        await db.SaveChangesAsync();

        var realtime = new TripSharingServiceTests.RealtimeFake();
        var scheduler = new TripSharingServiceTests.ExpirySchedulerFake();
        var delivery = new TripSharingServiceTests.NotificationDeliveryFake();
        var service = new TripSharingService(
            db,
            new InMemoryRedisService(),
            realtime,
            new TripSharingServiceTests.MutableClock { UtcNow = now },
            new TripSharingServiceTests.OptionsMonitorFake<TripSharingOptions>(new TripSharingOptions
            {
                AppLinkBaseUrl = "https://example.test/trips",
                DefaultExpirationHours = 6,
                CompletedGraceMinutes = 15,
                CancelledGraceMinutes = 5
            }),
            scheduler,
            delivery,
            NullLogger<TripSharingService>.Instance);

        var created = await service.CreateAsync(trip.Id, owner.Id, recipient.PhoneNumber!);

        var persistedShare = await db.TripShares.SingleAsync();
        var persistedNotification = await db.Notifications.SingleAsync();
        Assert.Equal(persistedShare.Id, created.TripShareId);
        Assert.Equal(persistedShare.Id, persistedNotification.ReferenceId);
        Assert.Equal("TripShared", persistedNotification.NotificationType);
        Assert.Single(delivery.Events);
    }

    private static AspNetUser CreateUser(string phoneNumber, string name, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        UserName = phoneNumber,
        NormalizedUserName = phoneNumber,
        PhoneNumber = phoneNumber,
        PhoneNumberConfirmed = true,
        FullName = name,
        IsActive = true,
        CreatedAt = now
    };
}
