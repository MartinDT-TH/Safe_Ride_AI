using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace SafeRide.Infrastructure.Persistence;

public static class BookingFeatureSeeder
{
    private static readonly Guid CustomerId = Guid.Parse("8d3e426f-a485-4d72-981d-4056a13c8387");
    private static readonly Guid DriverId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static async Task SeedBookingFeaturesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        await SeedCustomerAsync(db, now, cancellationToken);
        await SeedDriverAsync(db, now, cancellationToken);

        var vehicle = await SeedCustomerVehicleAsync(db, now, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCustomerAsync(
        ApplicationDbContext db,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var exists = await db.Users.AnyAsync(x => x.Id == CustomerId, cancellationToken);

        if (exists)
        {
            return;
        }

        db.Users.Add(new AspNetUser
        {
            Id = CustomerId,
            UserName = "customer.test@srd.com",
            NormalizedUserName = "CUSTOMER.TEST@SRD.COM",
            Email = "customer.test@srd.com",
            NormalizedEmail = "CUSTOMER.TEST@SRD.COM",
            EmailConfirmed = true,
            PhoneNumber = "0900000000",
            PhoneNumberConfirmed = true,

            TwoFactorEnabled = false,
            LockoutEnabled = true,
            AccessFailedCount = 0,

            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),

            FullName = "Khách hàng Test Booking",
            AvatarUrl = null,
            IsActive = true,
            BanReason = null,
            Gender = Gender.Male.ToString(),
            DateOfBirth = new DateOnly(2000, 1, 1),

            CreatedAt = now,
            UpdatedAt = now
        });
    }

    private static async Task SeedDriverAsync(
        ApplicationDbContext db,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var driverExists = await db.Users.AnyAsync(x => x.Id == DriverId, cancellationToken);

        if (!driverExists)
        {
            db.Users.Add(new AspNetUser
            {
                Id = DriverId,
                UserName = "driver.test@srd.com",
                NormalizedUserName = "DRIVER.TEST@SRD.COM",
                Email = "driver.test@srd.com",
                NormalizedEmail = "DRIVER.TEST@SRD.COM",
                EmailConfirmed = true,
                PhoneNumber = "0900000001",
                PhoneNumberConfirmed = true,

                TwoFactorEnabled = false,
                LockoutEnabled = true,
                AccessFailedCount = 0,

                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),

                FullName = "Tài xế Test Booking",
                AvatarUrl = null,
                IsActive = true,
                BanReason = null,
                Gender = Gender.Male.ToString(),
                DateOfBirth = new DateOnly(1995, 1, 1),

                CreatedAt = now,
                UpdatedAt = now
            });

            await db.SaveChangesAsync(cancellationToken);
        }

        var driverProfile = await db.DriverProfiles
            .FirstOrDefaultAsync(x => x.DriverId == DriverId, cancellationToken);

        if (driverProfile is null)
        {
            db.DriverProfiles.Add(new DriverProfile
            {
                DriverId = DriverId,
                IdentityCardNumber = "012345678901",
                ExperienceYears = 3,
                HomeAddress = "Đà Nẵng",
                WorkStatus = DriverWorkStatus.Online,
                LastActiveAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            driverProfile.WorkStatus = DriverWorkStatus.Online;
            driverProfile.LastActiveAt = now;
            driverProfile.UpdatedAt = now;
        }

        var hasDrivingLicense = await db.DriverKycs
            .AnyAsync(x => x.DriverId == DriverId && x.DocumentType == KycDocumentType.DRIVING_LICENSE, cancellationToken);

        if (!hasDrivingLicense)
        {
            db.DriverKycs.Add(new DriverKyc
            {
                DriverId = DriverId,
                DocumentType = KycDocumentType.DRIVING_LICENSE,
                DocumentNumber = "GPLX-B-000001",
                LicenseClass = LicenseClass.B,

                FrontImageUrl = "https://example.com/mock-license-front.jpg",
                BackImageUrl = "https://example.com/mock-license-back.jpg",
                FileUrl = null,

                IssueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2).Date),
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(8).Date),

                KycStatus = KycStatus.Approved,
                CreatedAt = now,
                VerifiedAt = now,
                RejectionReason = null
            });
        }

        var hasWallet = await db.DriverWallets
            .AnyAsync(x => x.DriverId == DriverId, cancellationToken);

        if (!hasWallet)
        {
            db.DriverWallets.Add(new DriverWallet
            {
                DriverId = DriverId,
                CurrentBalance = 0
            });
        }
    }

    private static async Task<Vehicle> SeedCustomerVehicleAsync(
        ApplicationDbContext db,
        DateTime now,
        CancellationToken cancellationToken)
    {
        const string plateNumber = "43A-88888";

        var vehicle = await db.Vehicles
            .FirstOrDefaultAsync(x => x.PlateNumber == plateNumber, cancellationToken);

        if (vehicle is not null)
        {
            return vehicle;
        }

        vehicle = new Vehicle
        {
            OwnerUserId = CustomerId,
            PlateNumber = plateNumber,
            BrandModel = "Toyota Vios",
            RequiredLicenseClass = RequiredLicenseClass.B,
            VehicleType = VehicleType.Car,
            EngineType = EngineType.ICE,
            TransmissionType = TransmissionType.Automatic,
            Color = "Trắng",
            IsDeleted = false,
            CreatedAt = now
        };

        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(cancellationToken);

        return vehicle;
    }

}