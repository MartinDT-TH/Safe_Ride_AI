using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
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
    private readonly string _databaseName = $"saferide-auth-{Guid.NewGuid():N}";
    private SqliteConnection? _keeperConnection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=unused",
                ["ConnectionStrings:Redis"] = string.Empty,
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

            var connectionString =
                $"Data Source={_databaseName};Mode=Memory;Cache=Shared;Default Timeout=30";
            _keeperConnection = new SqliteConnection(connectionString);
            _keeperConnection.Open();

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
            CreateAuthSchema(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
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
            """);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _keeperConnection?.Dispose();
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
    public Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan expiration) =>
        Task.FromResult(_values.TryAdd(key, value));
    public Task<string?> GetAsync(string key) =>
        Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);
    public Task RemoveAsync(string key)
    {
        _values.TryRemove(key, out _);
        return Task.CompletedTask;
    }
    public Task<long> IncrementAsync(string key, TimeSpan expiration) =>
        Task.FromResult(_counters.AddOrUpdate(key, 1, (_, count) => count + 1));

    public Task GeoAddAsync(
        string key,
        double longitude,
        double latitude,
        string member) =>
        _geoStorage.GeoAddAsync(key, longitude, latitude, member);

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
