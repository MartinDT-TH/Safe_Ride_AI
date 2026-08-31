using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SafeRide.Infrastructure.Persistence;
using System.Text;

namespace SafeRide.IntegrationTests;

internal sealed class SqlServerFactAttribute : FactAttribute
{
    private const string RunSqlServerTestsVariable = "SAFERIDE_RUN_SQLSERVER_TESTS";
    private const string TestConnectionVariable = "SAFERIDE_TEST_SQLSERVER";

    public SqlServerFactAttribute()
    {
        var hasExplicitConnection =
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(TestConnectionVariable));
        var hasOptedIntoLocalDb = string.Equals(
            Environment.GetEnvironmentVariable(RunSqlServerTestsVariable),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (!hasExplicitConnection && !hasOptedIntoLocalDb)
        {
            Skip = $"Set {TestConnectionVariable}, or set {RunSqlServerTestsVariable}=true to use LocalDB.";
        }
    }
}

internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    internal const string ProviderTraitName = "DatabaseProvider";
    internal const string SqlServerProvider = "SQLServer";
    internal const string InMemoryProvider = "InMemory";
    internal const string SqliteProvider = "SQLite";

    private const string EnvironmentVariableName = "SAFERIDE_TEST_SQLSERVER";
    private const string DatabasePrefix = "SafeRide_Test_";
    private const string LocalDbConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True";

    private readonly string _connectionString;
    private readonly string _databaseName;

    private SqlServerTestDatabase(string purpose)
    {
        var baseConnectionString = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            baseConnectionString = LocalDbConnectionString;
        }

        var builder = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = CreateDatabaseName(purpose),
            AttachDBFilename = string.Empty,
            Pooling = false,
            ApplicationName = "SafeRide.IntegrationTests"
        };

        _databaseName = builder.InitialCatalog;
        _connectionString = builder.ConnectionString;
        AssertOwnsTestDatabase();
    }

    public string DatabaseName => _databaseName;

    public static async Task<SqlServerTestDatabase> CreateAsync(
        string purpose,
        CancellationToken cancellationToken = default)
    {
        var database = new SqlServerTestDatabase(purpose);
        try
        {
            await database.ResetAsync(cancellationToken);
            return database;
        }
        catch
        {
            await database.TryDeleteAsync(CancellationToken.None);
            throw;
        }
    }

    public static async Task<SqlServerTestDatabase> CreateCurrentModelAsync(
        string purpose,
        CancellationToken cancellationToken = default)
    {
        var database = new SqlServerTestDatabase(purpose);
        try
        {
            await database.ResetToCurrentModelAsync(cancellationToken);
            return database;
        }
        catch
        {
            await database.TryDeleteAsync(CancellationToken.None);
            throw;
        }
    }

    public ApplicationDbContext CreateDbContext()
    {
        AssertOwnsTestDatabase();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                _connectionString,
                sqlServer => sqlServer
                    .UseNetTopologySuite()
                    .EnableRetryOnFailure())
            .Options;
        return new ApplicationDbContext(options, new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        AssertOwnsTestDatabase();
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    private async Task ResetToCurrentModelAsync(CancellationToken cancellationToken)
    {
        AssertOwnsTestDatabase();
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await DeleteAsync(CancellationToken.None);
    }

    private async Task TryDeleteAsync(CancellationToken cancellationToken)
    {
        AssertOwnsTestDatabase();
        try
        {
            await DeleteAsync(cancellationToken);
        }
        catch (SqlException)
        {
            // Preserve the original test failure when cleanup cannot reach SQL Server.
        }
    }

    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        AssertOwnsTestDatabase();
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync(cancellationToken);
    }

    private void AssertOwnsTestDatabase()
    {
        var configuredDatabase = new SqlConnectionStringBuilder(_connectionString).InitialCatalog;
        if (!string.Equals(configuredDatabase, _databaseName, StringComparison.Ordinal)
            || !_databaseName.StartsWith(DatabasePrefix, StringComparison.Ordinal)
            || _databaseName.Length <= DatabasePrefix.Length)
        {
            throw new InvalidOperationException(
                "SQL Server integration tests may reset only a database owned by the test fixture.");
        }
    }

    private static string CreateDatabaseName(string purpose)
    {
        var safePurpose = new StringBuilder();
        foreach (var character in purpose)
        {
            if (char.IsAsciiLetterOrDigit(character) || character == '_')
            {
                safePurpose.Append(character);
            }

            if (safePurpose.Length == 40)
            {
                break;
            }
        }

        if (safePurpose.Length == 0)
        {
            safePurpose.Append("Integration");
        }

        return $"{DatabasePrefix}{safePurpose}_{Guid.NewGuid():N}";
    }
}
