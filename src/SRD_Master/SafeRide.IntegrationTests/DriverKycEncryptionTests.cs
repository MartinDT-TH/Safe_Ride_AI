using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.IntegrationTests;

[Trait(SqlServerTestDatabase.ProviderTraitName, SqlServerTestDatabase.SqliteProvider)]
public sealed class DriverKycEncryptionTests
{
    [Fact]
    public async Task ApprovedLicenseReader_MaterializesBeforeComparingEncryptedValues()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection, sqlite => sqlite.UseNetTopologySuite())
            .Options;
        await using var dbContext = new ApplicationDbContext(
            options,
            new EphemeralDataProtectionProvider());
        var licenseProperty = dbContext.Model
            .FindEntityType(typeof(DriverKyc))!
            .FindProperty(nameof(DriverKyc.LicenseClass))!;
        Assert.Null(licenseProperty.GetMaxLength());
        Assert.Equal("nvarchar(max)", licenseProperty.GetColumnType());
        await CreateDriverKycTableAsync(dbContext);

        var driverId = Guid.NewGuid();
        var latestLicense = NewLicense(
            driverId,
            LicenseClass.B,
            new DateOnly(2030, 6, 15),
            KycStatus.Approved,
            new DateTime(2026, 6, 15, 8, 0, 0, DateTimeKind.Utc));
        dbContext.DriverKycs.AddRange(
            NewLicense(
                driverId,
                LicenseClass.A1,
                new DateOnly(2028, 6, 15),
                KycStatus.Approved,
                new DateTime(2026, 6, 14, 8, 0, 0, DateTimeKind.Utc)),
            latestLicense,
            NewLicense(
                driverId,
                LicenseClass.Old_B2,
                new DateOnly(2040, 6, 15),
                KycStatus.Rejected,
                new DateTime(2026, 6, 16, 8, 0, 0, DateTimeKind.Utc)));
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT LicenseClass, ExpiryDate FROM DriverKyc WHERE Id = $id";
            command.Parameters.AddWithValue("$id", latestLicense.Id);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.True(reader.GetString(0).Length > 10);
            Assert.NotEqual("2030-06-15", reader.GetString(1));
        }

        var licenses = await dbContext.LoadApprovedDrivingLicensesAsync(
            driverId,
            CancellationToken.None);

        Assert.Equal(2, licenses.Count);
        Assert.Contains(licenses, license => license.LicenseClass == LicenseClass.A1);
        Assert.Contains(licenses, license => license.LicenseClass == LicenseClass.B);
        Assert.Equal(new DateOnly(2030, 6, 15), licenses.GetLatestExpiryDate());
        Assert.Equal(
            new DateOnly(2028, 6, 15),
            licenses.GetLatestExpiredExpiryDate(new DateOnly(2029, 1, 1)));
        Assert.True(licenses.Single(x => x.LicenseClass == LicenseClass.B)
            .IsUsableOn(new DateOnly(2029, 1, 1)));
    }

    private static DriverKyc NewLicense(
        Guid driverId,
        LicenseClass licenseClass,
        DateOnly expiryDate,
        KycStatus status,
        DateTime verifiedAt)
    {
        return new DriverKyc
        {
            DriverId = driverId,
            DocumentType = KycDocumentType.DRIVING_LICENSE,
            DocumentNumber = $"LICENSE-{licenseClass}",
            LicenseClass = licenseClass,
            FrontImageUrl = $"https://example.test/{licenseClass}.jpg",
            IssueDate = new DateOnly(2025, 1, 1),
            ExpiryDate = expiryDate,
            KycStatus = status,
            CreatedAt = verifiedAt.AddMinutes(-1),
            VerifiedAt = verifiedAt
        };
    }

    private static Task CreateDriverKycTableAsync(ApplicationDbContext dbContext)
    {
        return dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE DriverKyc (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                DriverId TEXT NOT NULL,
                DocumentType TEXT NOT NULL,
                DocumentNumber TEXT NULL,
                FullName TEXT NULL,
                DateOfBirth TEXT NULL,
                Gender TEXT NULL,
                Address TEXT NULL,
                LicenseClass TEXT NULL,
                FrontImageUrl TEXT NULL,
                BackImageUrl TEXT NULL,
                FileUrl TEXT NULL,
                IssueDate TEXT NULL,
                ExpiryDate TEXT NULL,
                KycStatus TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                VerifiedAt TEXT NULL,
                RejectionReason TEXT NULL
            );
            """);
    }
}
