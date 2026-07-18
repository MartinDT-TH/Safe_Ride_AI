using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Infrastructure.Persistence;

public static class CustomerIdentitySeeder
{
    private static readonly TestCustomerSeed[] TestCustomers =
    [
        new(
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            "customer.anh.safe@srd.test",
            "0912000001",
            "Nguyen Minh Anh",
            Gender.Female,
            new DateOnly(1998, 5, 12),
            true,
            "43A-10001",
            "VinFast VF e34",
            RequiredLicenseClass.B,
            VehicleType.Car,
            EngineType.EV,
            TransmissionType.Automatic,
            null,
            "Trang"),
        new(
            Guid.Parse("20000000-0000-0000-0000-000000000002"),
            "customer.binh.safe@srd.test",
            "0912000002",
            "Tran Quoc Binh",
            Gender.Male,
            new DateOnly(1994, 11, 3),
            true,
            "43H1-20002",
            "Honda Vision",
            RequiredLicenseClass.A1,
            VehicleType.Motorbike,
            EngineType.ICE,
            TransmissionType.Automatic,
            110,
            "Do"),
        new(
            Guid.Parse("20000000-0000-0000-0000-000000000003"),
            "customer.chi.safe@srd.test",
            "0912000003",
            "Le Thu Chi",
            Gender.Female,
            new DateOnly(2000, 2, 20),
            false,
            "43K1-30003",
            "Yamaha Grande",
            RequiredLicenseClass.A1,
            VehicleType.Motorbike,
            EngineType.ICE,
            TransmissionType.Automatic,
            125,
            "Red"),
        new(
            Guid.Parse("20000000-0000-0000-0000-000000000004"),
            "customer.dung.safe@srd.test",
            "0912000004",
            "Pham Gia Dung",
            Gender.Other,
            new DateOnly(1996, 8, 14),
            true,
            "43A-40004",
            "Toyota Vios",
            RequiredLicenseClass.B,
            VehicleType.Car,
            EngineType.ICE,
            TransmissionType.Automatic,
            1496,
            "Black")
    ];

    public static async Task SeedCustomerIdentityAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!environment.IsDevelopment())
        {
            return;
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AspNetUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        foreach (var seed in TestCustomers)
        {
            var user = await userManager.FindByIdAsync(seed.CustomerId.ToString());
            if (user is null)
            {
                user = new AspNetUser
                {
                    Id = seed.CustomerId,
                    UserName = seed.Email,
                    Email = seed.Email,
                    EmailConfirmed = true,
                    PhoneNumber = seed.PhoneNumber,
                    PhoneNumberConfirmed = true,
                    FullName = seed.FullName,
                    IsActive = seed.IsActive,
                    Gender = seed.Gender.ToString(),
                    DateOfBirth = seed.DateOfBirth,
                    BanReason = seed.IsActive ? null : "Khóa thử nghiệm bởi hệ thống seed",
                    CreatedAt = now.AddDays(-Array.IndexOf(TestCustomers, seed) - 1),
                    UpdatedAt = now
                };

                var createResult = await userManager.CreateAsync(user, "SafeRide@12345");
                if (!createResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not seed test customer {seed.Email}: {string.Join("; ", createResult.Errors.Select(x => x.Description))}");
                }
            }
            else
            {
                user.UserName = seed.Email;
                user.Email = seed.Email;
                user.EmailConfirmed = true;
                user.PhoneNumber = seed.PhoneNumber;
                user.PhoneNumberConfirmed = true;
                user.FullName = seed.FullName;
                user.IsActive = seed.IsActive;
                user.Gender = seed.Gender.ToString();
                user.DateOfBirth = seed.DateOfBirth;
                user.BanReason = seed.IsActive ? null : "Khóa thử nghiệm bởi hệ thống seed";
                user.UpdatedAt = now;

                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not update test customer {seed.Email}: {string.Join("; ", updateResult.Errors.Select(x => x.Description))}");
                }
            }

            if (!await userManager.IsInRoleAsync(user, "Customer"))
            {
                var roleResult = await userManager.AddToRoleAsync(user, "Customer");
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not assign Customer role to {seed.Email}: {string.Join("; ", roleResult.Errors.Select(x => x.Description))}");
                }
            }

            await EnsureVehicleAsync(dbContext, seed, now, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureVehicleAsync(
        ApplicationDbContext dbContext,
        TestCustomerSeed seed,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var vehicle = await dbContext.Vehicles.FirstOrDefaultAsync(
            x => x.OwnerUserId == seed.CustomerId && !x.IsDeleted,
            cancellationToken);

        if (vehicle is null)
        {
            dbContext.Vehicles.Add(new Vehicle
            {
                OwnerUserId = seed.CustomerId,
                PlateNumber = seed.PlateNumber,
                BrandModel = seed.BrandModel,
                RequiredLicenseClass = seed.RequiredLicenseClass,
                VehicleType = seed.VehicleType,
                EngineType = seed.EngineType,
                TransmissionType = seed.TransmissionType,
                EngineCapacityCc = seed.EngineCapacityCc,
                Color = seed.Color,
                IsDeleted = false,
                CreatedAt = now
            });
            return;
        }

        vehicle.PlateNumber = seed.PlateNumber;
        vehicle.BrandModel = seed.BrandModel;
        vehicle.RequiredLicenseClass = seed.RequiredLicenseClass;
        vehicle.VehicleType = seed.VehicleType;
        vehicle.EngineType = seed.EngineType;
        vehicle.TransmissionType = seed.TransmissionType;
        vehicle.EngineCapacityCc = seed.EngineCapacityCc;
        vehicle.Color = seed.Color;
        vehicle.IsDeleted = false;
    }

    private sealed class TestCustomerSeed
    {
        public TestCustomerSeed(
            Guid customerId,
            string email,
            string phoneNumber,
            string fullName,
            Gender gender,
            DateOnly dateOfBirth,
            bool isActive,
            string plateNumber,
            string brandModel,
            RequiredLicenseClass requiredLicenseClass,
            VehicleType vehicleType,
            EngineType engineType,
            TransmissionType transmissionType,
            int? engineCapacityCc,
            string color)
        {
            CustomerId = customerId;
            Email = email;
            PhoneNumber = phoneNumber;
            FullName = fullName;
            Gender = gender;
            DateOfBirth = dateOfBirth;
            IsActive = isActive;
            PlateNumber = plateNumber;
            BrandModel = brandModel;
            RequiredLicenseClass = requiredLicenseClass;
            VehicleType = vehicleType;
            EngineType = engineType;
            TransmissionType = transmissionType;
            EngineCapacityCc = engineCapacityCc;
            Color = color;
        }

        public Guid CustomerId { get; }
        public string Email { get; }
        public string PhoneNumber { get; }
        public string FullName { get; }
        public Gender Gender { get; }
        public DateOnly DateOfBirth { get; }
        public bool IsActive { get; }
        public string PlateNumber { get; }
        public string BrandModel { get; }
        public RequiredLicenseClass RequiredLicenseClass { get; }
        public VehicleType VehicleType { get; }
        public EngineType EngineType { get; }
        public TransmissionType TransmissionType { get; }
        public int? EngineCapacityCc { get; }
        public string Color { get; }
    }
}
