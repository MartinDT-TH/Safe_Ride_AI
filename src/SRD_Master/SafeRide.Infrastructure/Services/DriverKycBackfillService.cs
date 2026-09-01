using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

/// One-shot backfill for rows created before DriverKyc encryption was enabled.
public sealed class DriverKycBackfillService(ApplicationDbContext db, IPiiProtectionService pii)
{
    private static readonly string[] Columns = ["DocumentNumber", "FullName", "DateOfBirth", "Gender", "Address", "LicenseClass", "FrontImageUrl", "BackImageUrl", "FileUrl", "IssueDate", "ExpiryDate"];

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        await using var select = connection.CreateCommand();
        select.Transaction = tx.GetDbTransaction();
        select.CommandText = $"SELECT [Id], {string.Join(",", Columns.Select(x => $"[{x}]"))} FROM [DriverKyc]";
        var count = 0;
        await using var reader = await select.ExecuteReaderAsync(cancellationToken);
        var rows = new List<(long Id, string?[] Values)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var values = Enumerable.Range(0, Columns.Length).Select(i => reader.IsDBNull(i + 1) ? null : reader.GetValue(i + 1)?.ToString()).ToArray();
            rows.Add((reader.GetInt64(0), values));
        }
        await reader.DisposeAsync();
        foreach (var row in rows)
        {
            var values = row.Values.Select(ProtectIfLegacy).ToArray();
            if (values.SequenceEqual(row.Values)) continue;
            await using var update = connection.CreateCommand();
            update.Transaction = tx.GetDbTransaction();
            update.CommandText = $"UPDATE [DriverKyc] SET {string.Join(",", Columns.Select((x, i) => $"[{x}]=@p{i}"))} WHERE [Id]=@id";
            for (var i = 0; i < values.Length; i++) { var p = update.CreateParameter(); p.ParameterName = $"@p{i}"; p.Value = (object?)values[i] ?? DBNull.Value; update.Parameters.Add(p); }
            var id = update.CreateParameter(); id.ParameterName = "@id"; id.Value = row.Id; update.Parameters.Add(id);
            await update.ExecuteNonQueryAsync(cancellationToken); count++;
        }
        await tx.CommitAsync(cancellationToken);
        return count;
    }

    private string? ProtectIfLegacy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var plain = pii.Unprotect(value);
        return string.Equals(plain, value, StringComparison.Ordinal) ? pii.Protect(value) : value;
    }
}
