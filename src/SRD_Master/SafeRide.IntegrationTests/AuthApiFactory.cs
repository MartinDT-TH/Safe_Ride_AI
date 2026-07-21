using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SafeRide.Application.Features.Auth.Services;
using SafeRide.Infrastructure.Persistence;
using SafeRide.Infrastructure.Redis;
using System.Collections.Concurrent;

namespace SafeRide.IntegrationTests;

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"saferide-auth-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("BackgroundJobs:Enabled", "false");
        builder.ConfigureLogging(logging => { /* logging.ClearProviders(); */ });
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=unused",
                ["ConnectionStrings:Redis"] = string.Empty,
                ["BackgroundJobs:Enabled"] = "false",
                ["Jwt:Issuer"] = "SafeRide.Tests",
                ["Jwt:Audience"] = "SafeRide.Tests.Client",
                ["Jwt:SecretKey"] = "integration-test-secret-key-that-is-long-enough-123456",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "30",
                ["GoogleMaps:ApiKey"] = "test-google-maps-key",
                ["GoogleMaps:RoutesApiUrl"] = "https://routes.googleapis.com/directions/v2:computeRoutes",
                ["GoogleMaps:GeocodingApiUrl"] = "https://maps.googleapis.com/maps/api/geocode/json",
                ["OpenRouteService:DirectionsApiUrl"] = "https://api.openrouteservice.org/v2/directions/driving-car",
                ["OpenRouteService:MatrixApiUrl"] = "https://api.openrouteservice.org/v2/matrix/driving-car"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IRedisService>();
            services.RemoveAll<ISpeedSmsService>();
            services.RemoveAll<IGoogleTokenVerifier>();

            var connectionString = $"Data Source={_databasePath};Default Timeout=30";

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlite(connectionString, sqlite => sqlite.UseNetTopologySuite()));
            services.AddSingleton<IRedisService, FakeRedisService>();
            services.AddSingleton<FakeSmsService>();
            services.AddSingleton<ISpeedSmsService>(
                provider => provider.GetRequiredService<FakeSmsService>());
            services.AddSingleton<FakeGoogleTokenVerifier>();
            services.AddSingleton<IGoogleTokenVerifier>(
                provider => provider.GetRequiredService<FakeGoogleTokenVerifier>());

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            CreateAuthSchema(dbContext);
            
            var roleManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<SafeRide.Domain.Entities.AspNetRole>>();
            roleManager.CreateAsync(new SafeRide.Domain.Entities.AspNetRole { Id = Guid.NewGuid(), Name = "Customer" }).GetAwaiter().GetResult();
            roleManager.CreateAsync(new SafeRide.Domain.Entities.AspNetRole { Id = Guid.NewGuid(), Name = "Driver" }).GetAwaiter().GetResult();
        });
    }

    private static void CreateAuthSchema(ApplicationDbContext dbContext)
    {
        dbContext.Database.ExecuteSqlRaw(
            """
            CREATE TABLE AspNetRoles (
                Id TEXT NOT NULL PRIMARY KEY, Description TEXT NULL, Name TEXT NULL,
                NormalizedName TEXT NULL, ConcurrencyStamp TEXT NULL
            );
            CREATE UNIQUE INDEX RoleNameIndex ON AspNetRoles (NormalizedName);
            CREATE TABLE AspNetUsers (
                Id TEXT NOT NULL PRIMARY KEY, FullName TEXT NULL, AvatarUrl TEXT NULL,
                IsActive INTEGER NOT NULL, BanReason TEXT NULL, Gender TEXT NULL,
                DateOfBirth TEXT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NULL,
                UserName TEXT NULL, NormalizedUserName TEXT NULL, Email TEXT NULL,
                NormalizedEmail TEXT NULL, EmailConfirmed INTEGER NOT NULL,
                PasswordHash TEXT NULL, SecurityStamp TEXT NULL, ConcurrencyStamp TEXT NULL,
                PhoneNumber TEXT NULL, PhoneNumberConfirmed INTEGER NOT NULL,
                TwoFactorEnabled INTEGER NOT NULL, LockoutEnd TEXT NULL,
                LockoutEnabled INTEGER NOT NULL, AccessFailedCount INTEGER NOT NULL
            );
            CREATE UNIQUE INDEX UserNameIndex ON AspNetUsers (NormalizedUserName);
            CREATE INDEX EmailIndex ON AspNetUsers (NormalizedEmail);
            CREATE TABLE AspNetUserRoles (
                UserId TEXT NOT NULL, RoleId TEXT NOT NULL, PRIMARY KEY (UserId, RoleId)
            );
            CREATE TABLE AspNetUserLogins (
                LoginProvider TEXT NOT NULL, ProviderKey TEXT NOT NULL,
                ProviderDisplayName TEXT NULL, UserId TEXT NOT NULL,
                PRIMARY KEY (LoginProvider, ProviderKey)
            );
            CREATE INDEX IX_AspNetUserLogins_UserId ON AspNetUserLogins (UserId);
            CREATE TABLE AspNetUserClaims (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL, ClaimType TEXT NULL, ClaimValue TEXT NULL
            );
            CREATE TABLE AspNetRoleClaims (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                RoleId TEXT NOT NULL, ClaimType TEXT NULL, ClaimValue TEXT NULL
            );
            CREATE TABLE AspNetUserTokens (
                UserId TEXT NOT NULL, LoginProvider TEXT NOT NULL,
                Name TEXT NOT NULL, Value TEXT NULL,
                PRIMARY KEY (UserId, LoginProvider, Name)
            );
            CREATE TABLE RefreshTokens (
                Id TEXT NOT NULL PRIMARY KEY, UserId TEXT NOT NULL,
                SessionId TEXT NOT NULL, TokenHash BLOB NOT NULL, JwtId TEXT NULL,
                DeviceId TEXT NULL, DeviceName TEXT NULL, CreatedAt TEXT NOT NULL,
                ExpiresAt TEXT NOT NULL, RevokedAt TEXT NULL,
                ReplacedByTokenHash BLOB NULL, IsRevoked INTEGER NOT NULL DEFAULT 0
            );
            CREATE UNIQUE INDEX IX_RefreshTokens_TokenHash ON RefreshTokens (TokenHash);
            CREATE INDEX IX_RefreshTokens_UserId ON RefreshTokens (UserId);
            CREATE INDEX IX_RefreshTokens_SessionId_RevokedAt
                ON RefreshTokens (SessionId, RevokedAt);
            CREATE TABLE DriverProfiles (
                DriverId TEXT NOT NULL PRIMARY KEY,
                IdentityCardNumber TEXT NOT NULL,
                ExperienceYears INTEGER NULL,
                HomeAddress TEXT NULL,
                WorkStatus TEXT NOT NULL DEFAULT 'Offline',
                LastActiveAt TEXT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NULL
            );
            CREATE TABLE DriverKyc (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                DriverId TEXT NOT NULL,
                DocumentType TEXT NOT NULL,
                DocumentNumber TEXT NULL,
                LicenseClass TEXT NULL,
                FrontImageUrl TEXT NULL,
                BackImageUrl TEXT NULL,
                FileUrl TEXT NULL,
                IssueDate TEXT NULL,
                ExpiryDate TEXT NULL,
                KycStatus TEXT NOT NULL DEFAULT 'Pending',
                CreatedAt TEXT NOT NULL,
                VerifiedAt TEXT NULL,
                RejectionReason TEXT NULL
            );
            CREATE INDEX IX_DriverKyc_DriverId ON DriverKyc (DriverId);
            CREATE TABLE Bookings (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                CustomerId TEXT NOT NULL,
                BookingStatus TEXT NOT NULL DEFAULT 'Searching',
                BookingType TEXT NOT NULL DEFAULT 'Now',
                PickupAddress TEXT NOT NULL DEFAULT '',
                PickupLocation BLOB NULL,
                DestinationAddress TEXT NULL,
                DestinationLocation BLOB NULL,
                EstimatedDistanceKm REAL NOT NULL DEFAULT 0,
                EstimatedDurationMinutes INTEGER NOT NULL DEFAULT 0,
                EstimatedFare TEXT NOT NULL DEFAULT '0',
                OriginalFare TEXT NOT NULL DEFAULT '0',
                DiscountAmount TEXT NOT NULL DEFAULT '0',
                FinalFare TEXT NOT NULL DEFAULT '0',
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            CREATE INDEX IX_Bookings_CustomerId ON Bookings (CustomerId);
            CREATE TABLE Trips (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                BookingId INTEGER NOT NULL,
                DriverId TEXT NOT NULL,
                TripStatus TEXT NOT NULL DEFAULT 'ACCEPTED',
                CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                DriverAssignedAt TEXT NULL,
                StartedAt TEXT NULL,
                CompletedAt TEXT NULL
            );
            CREATE INDEX IX_Trips_BookingId ON Trips (BookingId);
            CREATE INDEX IX_Trips_DriverId ON Trips (DriverId);
            CREATE TABLE Vehicles (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                OwnerUserId TEXT NOT NULL,
                PlateNumber TEXT NOT NULL,
                BrandModel TEXT NOT NULL,
                RequiredLicenseClass TEXT NOT NULL,
                VehicleType TEXT NOT NULL,
                EngineType TEXT NOT NULL,
                TransmissionType TEXT NOT NULL,
                EngineCapacityCc INTEGER NULL,
                Color TEXT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL
            );
            CREATE INDEX IX_Vehicles_OwnerUserId ON Vehicles (OwnerUserId);
            CREATE TABLE ServiceTypes (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                ServiceName TEXT NOT NULL
            );
            CREATE TABLE PricingRules (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                VehicleClass TEXT NOT NULL,
                ServiceTypeId INTEGER NOT NULL,
                BaseFare TEXT NOT NULL,
                MinFare TEXT NOT NULL,
                PricePerKm TEXT NULL,
                PricePerHour TEXT NULL,
                IsActive INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NULL
            );
            CREATE TABLE SurgePricingRules (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                RuleName TEXT NOT NULL,
                StartTime TEXT NOT NULL,
                EndTime TEXT NOT NULL,
                AppliedDays TEXT NOT NULL,
                SurgeMultiplier TEXT NOT NULL,
                IsActive INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NULL
            );
            CREATE TABLE Promotions (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                PromotionCode TEXT NOT NULL,
                DiscountType TEXT NOT NULL,
                DiscountValue TEXT NOT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL,
                MaxUsageCount INTEGER NOT NULL,
                CurrentUsageCount INTEGER NOT NULL,
                MinimumOrderValue TEXT NULL,
                MaximumDiscountValue TEXT NULL,
                UsageLimitPerUser INTEGER NOT NULL,
                IsActive INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NULL
            );
            """);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            DeleteDatabaseFile(_databasePath);
            DeleteDatabaseFile($"{_databasePath}-wal");
            DeleteDatabaseFile($"{_databasePath}-shm");
        }
    }

    private static void DeleteDatabaseFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

public sealed class FakeRedisService : IRedisService
{
    private readonly ConcurrentDictionary<string, string> _values = new();
    private readonly ConcurrentDictionary<string, long> _counters = new();
    private readonly InMemoryRedisService _geoStorage = new();
    public Task SetAsync(string key, string value, TimeSpan expiration)
    {
        _values[key] = value;
        return Task.CompletedTask;
    }
    public Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan expiration)
    {
        if (key.Contains("cooldown", StringComparison.OrdinalIgnoreCase)) return Task.FromResult(true);
        return Task.FromResult(_values.TryAdd(key, value));
    }

    public Task<bool> TryAcquireDistributedLockAsync(
        string key,
        string value,
        TimeSpan expiration) =>
        Task.FromResult(_values.TryAdd(key, value));

    public Task<string?> GetAsync(string key) =>
        Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

    public Task<IReadOnlyDictionary<string, string?>> GetManyAsync(
        IReadOnlyCollection<string> keys)
    {
        return Task.FromResult<IReadOnlyDictionary<string, string?>>(
            keys
                .Distinct(StringComparer.Ordinal)
                .ToDictionary(
                key => key,
                key => _values.TryGetValue(key, out var value) ? value : null));
    }

    public Task RemoveAsync(string key)
    {
        _values.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task ExpireAsync(
        string key,
        TimeSpan expiration,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task ListRightPushTrimAndExpireAsync(
        string key,
        string value,
        int maxLength,
        TimeSpan expiration,
        CancellationToken cancellationToken = default) =>
        _geoStorage.ListRightPushTrimAndExpireAsync(
            key,
            value,
            maxLength,
            expiration,
            cancellationToken);

    public Task<IReadOnlyList<string>> ListRangeAsync(
        string key,
        long start = 0,
        long stop = -1,
        CancellationToken cancellationToken = default) =>
        _geoStorage.ListRangeAsync(key, start, stop, cancellationToken);

    public Task<long> IncrementAsync(string key, TimeSpan expiration) =>
        Task.FromResult(_counters.AddOrUpdate(key, 1, (_, count) => count + 1));

    public Task GeoAddAsync(
        string key,
        double longitude,
        double latitude,
        string member) =>
        _geoStorage.GeoAddAsync(key, longitude, latitude, member);

    public Task GeoRemoveAsync(
        string key,
        string member,
        CancellationToken cancellationToken = default) =>
        _geoStorage.GeoRemoveAsync(key, member, cancellationToken);

    public Task<IReadOnlyList<string>> GeoRadiusAsync(
        string key,
        double longitude,
        double latitude,
        double radiusKm,
        int count) =>
        _geoStorage.GeoRadiusAsync(
            key,
            longitude,
            latitude,
            radiusKm,
            count);

    public Task<OtpVerificationResult> VerifyAndConsumeOtpAsync(
        string otpKey,
        string attemptsKey,
        string expectedHash,
        int maxAttempts)
    {
        if (!_values.TryGetValue(otpKey, out var stored))
        {
            return Task.FromResult(OtpVerificationResult.Missing);
        }

        var attempts = (int)_counters.GetValueOrDefault(attemptsKey);
        if (attempts >= maxAttempts)
        {
            _values.TryRemove(otpKey, out _);
            return Task.FromResult(OtpVerificationResult.AttemptsExceeded);
        }

        if (!string.Equals(stored, expectedHash, StringComparison.Ordinal))
        {
            attempts = (int)_counters.AddOrUpdate(attemptsKey, 1, (_, count) => count + 1);
            if (attempts >= maxAttempts)
            {
                _values.TryRemove(otpKey, out _);
                return Task.FromResult(OtpVerificationResult.AttemptsExceeded);
            }
            return Task.FromResult(OtpVerificationResult.Invalid);
        }

        _values.TryRemove(otpKey, out _);
        _counters.TryRemove(attemptsKey, out _);
        return Task.FromResult(OtpVerificationResult.Success);
    }

    public Task<TripTrackingUpdateResult> RecordTripTrackingPointAsync(
        TripTrackingPoint point,
        TripTrackingWriteOptions options,
        CancellationToken cancellationToken = default) =>
        _geoStorage.RecordTripTrackingPointAsync(point, options, cancellationToken);

    public Task<TripTrackingSnapshot> GetTripTrackingSnapshotAsync(
        long tripId,
        CancellationToken cancellationToken = default) =>
        _geoStorage.GetTripTrackingSnapshotAsync(tripId, cancellationToken);

    public Task RemoveTripTrackingAsync(
        long tripId,
        CancellationToken cancellationToken = default) =>
        _geoStorage.RemoveTripTrackingAsync(tripId, cancellationToken);
}

public sealed class FakeSmsService : ISpeedSmsService
{
    public ConcurrentDictionary<string, string> Codes { get; } = new();

    public Task SendOtpAsync(string phoneNumber, string otpCode)
    {
        Codes[phoneNumber] = otpCode;
        return Task.CompletedTask;
    }
}

public sealed class FakeGoogleTokenVerifier : IGoogleTokenVerifier
{
    public GoogleUserInfo User { get; set; } = new(
        "google-subject",
        "google@example.test",
        true,
        "Google User",
        "https://example.test/avatar.png");

    public Task<GoogleUserInfo> VerifyAsync(string idToken)
    {
        return Task.FromResult(User);
    }
}
