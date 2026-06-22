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
    private static readonly LicenseClass[] SupportedTestDriverLicenses =
    [
        LicenseClass.A1,
        LicenseClass.A,
        LicenseClass.B
    ];

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
            SupportedTestDriverLicenses,
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
            SupportedTestDriverLicenses,
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
            SupportedTestDriverLicenses,
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
            SupportedTestDriverLicenses,
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
            SupportedTestDriverLicenses,
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
            else
            {
                user.UserName = seed.Email;
                user.Email = seed.Email;
                user.EmailConfirmed = true;
                user.PhoneNumber = seed.PhoneNumber;
                user.PhoneNumberConfirmed = true;
                user.FullName = seed.FullName;
                user.IsActive = true;
                user.Gender = Gender.Male.ToString();
                user.UpdatedAt = now;

                var updateResult = await userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not update test driver {seed.Email}: {string.Join("; ", updateResult.Errors.Select(x => x.Description))}");
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
            else
            {
                profile.IdentityCardNumber = seed.IdentityCardNumber;
                profile.ExperienceYears = seed.ExperienceYears;
                profile.HomeAddress = string.IsNullOrWhiteSpace(profile.HomeAddress)
                    ? "Da Nang"
                    : profile.HomeAddress;

                if (profile.WorkStatus != DriverWorkStatus.Busy)
                {
                    profile.WorkStatus = DriverWorkStatus.Online;
                    profile.LastActiveAt = now;
                }

                profile.UpdatedAt = now;
            }

            await EnsureDriverKycAsync(
                dbContext,
                seed.DriverId,
                KycDocumentType.ID_CARD,
                seed.IdentityCardNumber,
                null,
                "https://example.test/srd-id-card-front.jpg",
                "https://example.test/srd-id-card-back.jpg",
                null,
                DateOnly.FromDateTime(now.AddYears(-5)),
                DateOnly.FromDateTime(now.AddYears(10)),
                now);

            foreach (var licenseClass in seed.LicenseClasses)
            {
                await EnsureDriverKycAsync(
                    dbContext,
                    seed.DriverId,
                    KycDocumentType.DRIVING_LICENSE,
                    $"{seed.LicenseNumber}-{licenseClass}",
                    licenseClass,
                    "https://example.test/srd-license-front.jpg",
                    "https://example.test/srd-license-back.jpg",
                    null,
                    DateOnly.FromDateTime(now.AddYears(-3)),
                    DateOnly.FromDateTime(now.AddYears(7)),
                    now);
            }

            await EnsureDriverKycAsync(
                dbContext,
                seed.DriverId,
                KycDocumentType.CRIMINAL_RECORD,
                $"SRD-CR-{seed.DriverId.ToString()[^6..].ToUpperInvariant()}",
                null,
                null,
                null,
                "https://example.test/srd-criminal-record.pdf",
                DateOnly.FromDateTime(now.AddMonths(-3)),
                DateOnly.FromDateTime(now.AddMonths(9)),
                now);

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

    private static async Task EnsureDriverKycAsync(
        ApplicationDbContext dbContext,
        Guid driverId,
        KycDocumentType documentType,
        string documentNumber,
        LicenseClass? licenseClass,
        string? frontImageUrl,
        string? backImageUrl,
        string? fileUrl,
        DateOnly issueDate,
        DateOnly expiryDate,
        DateTime now)
    {
        var existingKycQuery = dbContext.DriverKycs.Where(
            x => x.DriverId == driverId
                && x.DocumentType == documentType);
        if (documentType == KycDocumentType.DRIVING_LICENSE && licenseClass.HasValue)
        {
            existingKycQuery = existingKycQuery.Where(x => x.LicenseClass == licenseClass);
        }

        var existingKyc = await existingKycQuery.FirstOrDefaultAsync();

        if (existingKyc is null)
        {
            dbContext.DriverKycs.Add(new DriverKyc
            {
                DriverId = driverId,
                DocumentType = documentType,
                DocumentNumber = documentNumber,
                LicenseClass = licenseClass,
                FrontImageUrl = frontImageUrl,
                BackImageUrl = backImageUrl,
                FileUrl = fileUrl,
                IssueDate = issueDate,
                ExpiryDate = expiryDate,
                KycStatus = KycStatus.Approved,
                CreatedAt = now,
                VerifiedAt = now,
                RejectionReason = null
            });

            return;
        }

        existingKyc.DocumentNumber = documentNumber;
        existingKyc.LicenseClass = licenseClass;
        existingKyc.FrontImageUrl = frontImageUrl;
        existingKyc.BackImageUrl = backImageUrl;
        existingKyc.FileUrl = fileUrl;
        existingKyc.IssueDate = issueDate;
        existingKyc.ExpiryDate = expiryDate;
        existingKyc.KycStatus = KycStatus.Approved;
        existingKyc.VerifiedAt ??= now;
        existingKyc.RejectionReason = null;
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

    private sealed class TestDriverSeed
    {
        public TestDriverSeed(
            Guid driverId,
            string email,
            string phoneNumber,
            string fullName,
            string identityCardNumber,
            string licenseNumber,
            IReadOnlyList<LicenseClass> licenseClasses,
            double latitude,
            double longitude,
            int experienceYears)
        {
            DriverId = driverId;
            Email = email;
            PhoneNumber = phoneNumber;
            FullName = fullName;
            IdentityCardNumber = identityCardNumber;
            LicenseNumber = licenseNumber;
            LicenseClasses = licenseClasses;
            Latitude = latitude;
            Longitude = longitude;
            ExperienceYears = experienceYears;
        }

        public Guid DriverId { get; }
        public string Email { get; }
        public string PhoneNumber { get; }
        public string FullName { get; }
        public string IdentityCardNumber { get; }
        public string LicenseNumber { get; }
        public IReadOnlyList<LicenseClass> LicenseClasses { get; }
        public double Latitude { get; }
        public double Longitude { get; }
        public int ExperienceYears { get; }
    }
}
