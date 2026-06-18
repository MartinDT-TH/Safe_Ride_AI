using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Redis;
using System.Text.Json;

namespace SafeRide.Infrastructure.Persistence;

public static class IdentitySeeder
{
    private static readonly TestDriverSeed[] TestDrivers =
    [
        // ~0.8 km from center
        new(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            "driver.b.safe@srd.test",
            "0901000001",
            "SafeRide Test Driver B1",
            "SRD-ID-000001",
            "SRD-B-000001",
            LicenseClass.B,
            16.075691660288026,
            108.21881746818354,
            5),

        // ~2.5 km from center
        new(
            Guid.Parse("10000000-0000-0000-0000-000000000002"),
            "driver.b2.safe@srd.test",
            "0901000002",
            "SafeRide Test Driver B2",
            "SRD-ID-000002",
            "SRD-B-000002",
            LicenseClass.B,
            16.05470584882748,
            108.23006623372896,
            3),

        // ~5.0 km from center
        new(
            Guid.Parse("10000000-0000-0000-0000-000000000003"),
            "driver.a.safe@srd.test",
            "0901000003",
            "SafeRide Test Driver A",
            "SRD-ID-000003",
            "SRD-A-000003",
            LicenseClass.A,
            16.048117543791392,
            108.17300223966612,
            4),

        // ~9.0 km from center
        new(
            Guid.Parse("10000000-0000-0000-0000-000000000004"),
            "driver.a1.safe@srd.test",
            "0901000004",
            "SafeRide Test Driver A1",
            "SRD-ID-000004",
            "SRD-A1-000004",
            LicenseClass.A1,
            16.111061501803842,
            108.14056244597721,
            2),

        // ~14.5 km from center
        new(
            Guid.Parse("10000000-0000-0000-0000-000000000005"),
            "driver.oldb2.safe@srd.test",
            "0901000005",
            "SafeRide Test Driver Old B2",
            "SRD-ID-000005",
            "SRD-OLDB2-000005",
            LicenseClass.Old_B2,
            16.19313683009435,
            108.25996556885026,
            7)
    ];

    public static async Task SeedIdentityAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AspNetRole>>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

        await EnsureRoleAsync(roleManager, "Customer", "Default customer role");
        await EnsureRoleAsync(roleManager, "Driver", "Default driver role");

        if (environment.IsDevelopment())
        {
            await SeedRealtimeTestDriversAsync(scope.ServiceProvider);
        }
    }

    private static async Task EnsureRoleAsync(
        RoleManager<AspNetRole> roleManager,
        string roleName,
        string description)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result = await roleManager.CreateAsync(new AspNetRole
        {
            Id = Guid.NewGuid(),
            Name = roleName,
            Description = description
        });

        if (!result.Succeeded && !await roleManager.RoleExistsAsync(roleName))
        {
            throw new InvalidOperationException(
                $"Could not seed {roleName} role: {string.Join("; ", result.Errors.Select(x => x.Description))}");
        }
    }

    private static async Task SeedRealtimeTestDriversAsync(
        IServiceProvider serviceProvider)
    {
        var userManager = serviceProvider.GetRequiredService<UserManager<AspNetUser>>();
        var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var redisService = serviceProvider.GetRequiredService<IRedisService>();
        var now = DateTime.UtcNow;

        foreach (var seed in TestDrivers)
        {
            var user = await userManager.FindByIdAsync(seed.DriverId.ToString());
            if (user is null)
            {
                user = new AspNetUser
                {
                    Id = seed.DriverId,
                    UserName = seed.Email,
                    Email = seed.Email,
                    EmailConfirmed = true,
                    PhoneNumber = seed.PhoneNumber,
                    PhoneNumberConfirmed = true,
                    FullName = seed.FullName,
                    IsActive = true,
                    Gender = Gender.Male.ToString(),
                    DateOfBirth = new DateOnly(1990, 1, 1),
                    CreatedAt = now,
                    UpdatedAt = now
                };

                var createResult = await userManager.CreateAsync(
                    user,
                    "SafeRide@12345");
                if (!createResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not seed test driver {seed.Email}: {string.Join("; ", createResult.Errors.Select(x => x.Description))}");
                }
            }

            if (!await userManager.IsInRoleAsync(user, "Driver"))
            {
                var roleResult = await userManager.AddToRoleAsync(user, "Driver");
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not assign Driver role to {seed.Email}: {string.Join("; ", roleResult.Errors.Select(x => x.Description))}");
                }
            }

            var profile = await dbContext.DriverProfiles
                .FirstOrDefaultAsync(x => x.DriverId == seed.DriverId);
            if (profile is null)
            {
                dbContext.DriverProfiles.Add(new DriverProfile
                {
                    DriverId = seed.DriverId,
                    IdentityCardNumber = seed.IdentityCardNumber,
                    ExperienceYears = seed.ExperienceYears,
                    HomeAddress = "Da Nang",
                    WorkStatus = DriverWorkStatus.Online,
                    LastActiveAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            else if (profile.WorkStatus != DriverWorkStatus.Busy)
            {
                profile.WorkStatus = DriverWorkStatus.Online;
                profile.LastActiveAt = now;
                profile.UpdatedAt = now;
            }

            var hasDrivingLicense = await dbContext.DriverKycs.AnyAsync(
                x => x.DriverId == seed.DriverId
                    && x.DocumentType == KycDocumentType.DRIVING_LICENSE
                    && x.DocumentNumber == seed.LicenseNumber);
            if (!hasDrivingLicense)
            {
                dbContext.DriverKycs.Add(new DriverKyc
                {
                    DriverId = seed.DriverId,
                    DocumentType = KycDocumentType.DRIVING_LICENSE,
                    DocumentNumber = seed.LicenseNumber,
                    LicenseClass = seed.LicenseClass,
                    FrontImageUrl = "https://example.test/srd-license-front.jpg",
                    BackImageUrl = "https://example.test/srd-license-back.jpg",
                    IssueDate = DateOnly.FromDateTime(now.AddYears(-3)),
                    ExpiryDate = DateOnly.FromDateTime(now.AddYears(7)),
                    KycStatus = KycStatus.Approved,
                    CreatedAt = now,
                    VerifiedAt = now
                });
            }

            var hasWallet = await dbContext.DriverWallets.AnyAsync(
                x => x.DriverId == seed.DriverId);
            if (!hasWallet)
            {
                dbContext.DriverWallets.Add(new DriverWallet
                {
                    DriverId = seed.DriverId,
                    CurrentBalance = 0
                });
            }

            await CacheDriverOnlineAsync(redisService, seed, now);
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task CacheDriverOnlineAsync(
        IRedisService redisService,
        TestDriverSeed seed,
        DateTime now)
    {
        var location = new DriverLocationCache(
            seed.DriverId,
            seed.Latitude,
            seed.Longitude,
            now);

        await redisService.SetAsync(
            RedisKeys.DriverLocation(seed.DriverId),
            JsonSerializer.Serialize(location),
            TimeSpan.FromHours(12));
        await redisService.SetAsync(
            RedisKeys.DriverOnline(seed.DriverId),
            "1",
            TimeSpan.FromHours(12));
        await redisService.SetAsync(
            RedisKeys.DriverStatus(seed.DriverId),
            DriverWorkStatus.Online.ToString(),
            TimeSpan.FromHours(12));
        await redisService.GeoAddAsync(
            RedisKeys.OnlineDriversGeo,
            seed.Longitude,
            seed.Latitude,
            seed.DriverId.ToString());
    }

    private sealed record TestDriverSeed(
        Guid DriverId,
        string Email,
        string PhoneNumber,
        string FullName,
        string IdentityCardNumber,
        string LicenseNumber,
        LicenseClass LicenseClass,
        double Latitude,
        double Longitude,
        int ExperienceYears);
}
