using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Infrastructure.Persistence;
using System.Data;
using System.Security.Cryptography;

namespace SafeRide.Infrastructure.Services;

/// <summary>
/// One-shot migration from the pre-SafeRide application discriminator to the
/// current shared Data Protection key ring. It only changes values that the
/// legacy provider can decrypt successfully.
/// </summary>
public sealed class DriverKycKeyMigrationService(
    ApplicationDbContext db,
    IDataProtectionProvider provider,
    IPreviousDriverKycPiiProtectionService previous,
    ILegacyDriverKycPiiProtectionService legacy)
{
    private static readonly string[] Columns =
    [
        "DocumentNumber", "FullName", "DateOfBirth", "Gender", "Address",
        "LicenseClass", "FrontImageUrl", "BackImageUrl", "FileUrl",
        "IssueDate", "ExpiryDate"
    ];

    private readonly IDataProtector _current = provider.CreateProtector("SafeRide.DriverKyc.Pii.v1");

    public async Task<DriverKycKeyMigrationResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();

        await using var select = connection.CreateCommand();
        select.Transaction = transaction.GetDbTransaction();
        select.CommandText = $"SELECT [Id], {string.Join(",", Columns.Select(column => $"[{column}]"))} FROM [DriverKyc]";

        var rows = new List<(long Id, string?[] Values)>();
        await using (var reader = await select.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var values = Enumerable.Range(0, Columns.Length)
                    .Select(index => reader.IsDBNull(index + 1)
                        ? null
                        : reader.GetValue(index + 1)?.ToString())
                    .ToArray();
                rows.Add((reader.GetInt64(0), values));
            }
        }

        var migratedRows = 0;
        var migratedValues = 0;
        var skippedValues = 0;
        foreach (var row in rows)
        {
            var values = row.Values.ToArray();
            var changed = false;
            for (var index = 0; index < values.Length; index++)
            {
                var migration = Migrate(values[index]);
                skippedValues += migration.Skipped ? 1 : 0;
                if (!migration.Migrated)
                {
                    continue;
                }

                values[index] = migration.Value;
                changed = true;
                migratedValues++;
            }

            if (!changed)
            {
                continue;
            }

            await using var update = connection.CreateCommand();
            update.Transaction = transaction.GetDbTransaction();
            update.CommandText = $"UPDATE [DriverKyc] SET {string.Join(",", Columns.Select((column, index) => $"[{column}]=@p{index}"))} WHERE [Id]=@id";
            for (var index = 0; index < values.Length; index++)
            {
                var parameter = update.CreateParameter();
                parameter.ParameterName = $"@p{index}";
                parameter.Value = (object?)values[index] ?? DBNull.Value;
                update.Parameters.Add(parameter);
            }

            var id = update.CreateParameter();
            id.ParameterName = "@id";
            id.Value = row.Id;
            update.Parameters.Add(id);
            await update.ExecuteNonQueryAsync(cancellationToken);
            migratedRows++;
        }

        await transaction.CommitAsync(cancellationToken);
        return new DriverKycKeyMigrationResult(migratedRows, migratedValues, skippedValues);
    }

    private MigrationValue Migrate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new MigrationValue(value, false, false);
        }

        try
        {
            _current.Unprotect(value);
            return new MigrationValue(value, false, false);
        }
        catch (CryptographicException)
        {
            if (previous.TryUnprotect(value, out var previousPlaintext) && previousPlaintext is not null)
            {
                return new MigrationValue(_current.Protect(previousPlaintext), true, false);
            }

            if (legacy.TryUnprotect(value, out var plaintext) && plaintext is not null)
            {
                return new MigrationValue(_current.Protect(plaintext), true, false);
            }

            // Rows created before encryption was enabled can still contain
            // plaintext. Protect those values, but never overwrite unknown
            // Data Protection payloads that could not be opened.
            if (!value.StartsWith("CfDJ8", StringComparison.Ordinal))
            {
                return new MigrationValue(_current.Protect(value), true, false);
            }

            return new MigrationValue(value, false, true);
        }
    }

    private sealed record MigrationValue(string? Value, bool Migrated, bool Skipped);
}

public sealed record DriverKycKeyMigrationResult(
    int MigratedRows,
    int MigratedValues,
    int SkippedValues);
