using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Services;

namespace SafeRide.IntegrationTests;

[Trait(SqlServerTestDatabase.ProviderTraitName, SqlServerTestDatabase.InMemoryProvider)]
public sealed class AdminTripManagementServiceTests
{
    private static readonly DateTime UtcNow =
        new(2026, 7, 20, 8, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetTripDetailsByBookingIdAsync_ExistingTrip_ReturnsCompleteDetail()
    {
        await using var fixture = CreateFixture();
        var tripId = SeedCompletedTrip(fixture.DbContext);
        await fixture.DbContext.SaveChangesAsync();

        var detail = await fixture.Service.GetTripDetailsByBookingIdAsync(
            500,
            CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(tripId, detail.TripId);
        Assert.Equal(500, detail.BookingId);
        Assert.Equal(TripStatus.COMPLETED, detail.TripStatus);
        Assert.Equal("Nguyen Van A", detail.Customer.Name);
        Assert.Equal("Tran Tai Xe", detail.Driver.Name);
        Assert.Equal("Toyota Vios", detail.Vehicle.BrandModel);
        Assert.Equal(4.2m, detail.Route.ActualDistanceKm);
        Assert.Equal(56_000m, detail.Fare.FinalFare);
        Assert.Equal(11_000m, detail.Fare.DiscountAmount);
        Assert.Equal(PaymentStatus.Success, detail.Payment?.PaymentStatus);
        Assert.Single(detail.Promotions);
        Assert.Equal("SAFE20", detail.Promotions[0].PromotionCode);
        Assert.Equal(5, detail.Rating?.RatingScore);
        Assert.Equal("Chuyen di an toan.", detail.Rating?.Comment);
    }

    [Fact]
    public async Task GetTripDetailsByTripIdAsync_MissingTrip_ReturnsNull()
    {
        await using var fixture = CreateFixture();

        var detail = await fixture.Service.GetTripDetailsByTripIdAsync(
            404,
            CancellationToken.None);

        Assert.Null(detail);
    }

    private static AdminTripFixture CreateFixture()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"admin-trip-details-{Guid.NewGuid():N}")
            .Options;
        var dbContext = new ApplicationDbContext(options);

        return new AdminTripFixture(
            dbContext,
            new AdminTripManagementService(dbContext));
    }

    private static long SeedCompletedTrip(ApplicationDbContext dbContext)
    {
        var customerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var driverId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var customer = new AspNetUser
        {
            Id = customerId,
            UserName = "customer@example.test",
            Email = "customer@example.test",
            FullName = "Nguyen Van A",
            PhoneNumber = "+84901234567",
            IsActive = true,
            CreatedAt = UtcNow.AddDays(-2)
        };
        var driverUser = new AspNetUser
        {
            Id = driverId,
            UserName = "driver@example.test",
            Email = "driver@example.test",
            FullName = "Tran Tai Xe",
            PhoneNumber = "+84987654321",
            IsActive = true,
            CreatedAt = UtcNow.AddDays(-3)
        };
        var driver = new DriverProfile
        {
            DriverId = driverId,
            Driver = driverUser,
            IdentityCardNumber = "123456789",
            WorkStatus = DriverWorkStatus.Offline,
            ExperienceYears = 4,
            CreatedAt = UtcNow.AddDays(-3)
        };
        var serviceType = new ServiceType
        {
            Id = 1,
            ServiceName = "Theo chuyen"
        };
        var vehicle = new Vehicle
        {
            Id = 10,
            OwnerUserId = customerId,
            OwnerUser = customer,
            PlateNumber = "51G-123.45",
            BrandModel = "Toyota Vios",
            RequiredLicenseClass = RequiredLicenseClass.B,
            VehicleType = VehicleType.Car,
            EngineType = EngineType.ICE,
            TransmissionType = TransmissionType.Automatic,
            EngineCapacityCc = 1500,
            Color = "Trang",
            CreatedAt = UtcNow.AddDays(-2)
        };
        var promotion = new Promotion
        {
            Id = 20,
            PromotionCode = "SAFE20",
            DiscountType = DiscountType.Percentage,
            DiscountValue = 20m,
            StartDate = UtcNow.AddDays(-7),
            EndDate = UtcNow.AddDays(7),
            MaxUsageCount = 100,
            CurrentUsageCount = 1,
            MinimumOrderValue = 0m,
            MaximumDiscountValue = 50_000m,
            UsageLimitPerUser = 1,
            IsActive = true
        };
        var booking = new Booking
        {
            BookingId = 500,
            CustomerId = customerId,
            Customer = customer,
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            ServiceTypeId = serviceType.Id,
            ServiceType = serviceType,
            BookingType = BookingType.Now,
            BookingStatus = BookingStatus.Completed,
            PickupAddress = "Landmark 81",
            PickupLocation = new Point(106.7219, 10.7953) { SRID = 4326 },
            DestinationAddress = "Bitexco Tower",
            DestinationLocation = new Point(106.7042, 10.7717) { SRID = 4326 },
            EstimatedDistanceKm = 4.0m,
            EstimatedDurationMinutes = 25,
            EstimatedFare = 67_000m,
            SpecialRequest = "Goi truoc khi den.",
            CreatedAt = UtcNow.AddMinutes(-40),
            UpdatedAt = UtcNow.AddMinutes(-1)
        };
        booking.BookingPromotions.Add(new BookingPromotion
        {
            BookingId = booking.BookingId,
            Booking = booking,
            PromotionId = promotion.Id,
            Promotion = promotion,
            DiscountAmount = 11_000m,
            CreatedAt = UtcNow.AddMinutes(-39)
        });
        var trip = new Trip
        {
            Id = 900,
            BookingId = booking.BookingId,
            Booking = booking,
            DriverId = driverId,
            Driver = driver,
            TripStatus = TripStatus.COMPLETED,
            DriverAssignedAt = UtcNow.AddMinutes(-35),
            ArrivedAt = UtcNow.AddMinutes(-30),
            StartedAt = UtcNow.AddMinutes(-28),
            EndedAt = UtcNow,
            CompletedAt = UtcNow.AddMinutes(2),
            ActualDistanceKm = 4.2m,
            ActualDurationMinutes = 28,
            ActualFare = 67_000m,
            FinalFare = 56_000m,
            CreatedAt = UtcNow.AddMinutes(-35)
        };
        trip.Payments.Add(new Payment
        {
            Id = 700,
            TripId = trip.Id,
            Trip = trip,
            PaymentMethod = PaymentMethod.QR,
            PaymentStatus = PaymentStatus.Success,
            Amount = 56_000m,
            Currency = "VND",
            PaidAt = UtcNow.AddMinutes(3),
            CreatedAt = UtcNow.AddMinutes(1)
        });
        trip.Rating = new Rating
        {
            Id = 800,
            TripId = trip.Id,
            Trip = trip,
            CustomerId = customerId,
            Customer = customer,
            DriverId = driverId,
            Driver = driver,
            RatingScore = 5,
            Comment = "Chuyen di an toan.",
            CreatedAt = UtcNow.AddMinutes(4)
        };

        dbContext.AspNetUsers.AddRange(customer, driverUser);
        dbContext.DriverProfiles.Add(driver);
        dbContext.ServiceTypes.Add(serviceType);
        dbContext.Vehicles.Add(vehicle);
        dbContext.Promotions.Add(promotion);
        dbContext.Bookings.Add(booking);
        dbContext.Trips.Add(trip);

        return trip.Id;
    }

    private sealed record AdminTripFixture(
        ApplicationDbContext DbContext,
        AdminTripManagementService Service) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            return DbContext.DisposeAsync();
        }
    }
}
