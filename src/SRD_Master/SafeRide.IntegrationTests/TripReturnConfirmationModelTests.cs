using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.IntegrationTests;

public sealed class TripReturnConfirmationModelTests
{
    [Fact]
    public void Model_MapsReturnConfirmationAuditAndEvidenceRelationships()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection, sqlite => sqlite.UseNetTopologySuite())
            .Options;

        using var dbContext = new ApplicationDbContext(options);

        var model = dbContext.GetService<IDesignTimeModel>().Model;
        var confirmation = model.FindEntityType(typeof(TripReturnConfirmation));
        var evidence = model.FindEntityType(typeof(TripReturnEvidence));

        Assert.NotNull(confirmation);
        Assert.NotNull(evidence);
        Assert.Equal("TripReturnConfirmations", confirmation.GetTableName());
        Assert.Equal("TripReturnEvidence", evidence.GetTableName());

        Assert.Equal(
            30,
            confirmation.FindProperty(nameof(TripReturnConfirmation.HandoverStatus))?.GetMaxLength());
        Assert.Equal(
            "decimal(9, 6)",
            confirmation.FindProperty(nameof(TripReturnConfirmation.DriverLatitude))?.GetColumnType());
        Assert.Equal(
            "decimal(9, 6)",
            confirmation.FindProperty(nameof(TripReturnConfirmation.DriverLongitude))?.GetColumnType());
        Assert.Equal(
            1000,
            confirmation.FindProperty(nameof(TripReturnConfirmation.Note))?.GetMaxLength());
        Assert.Equal(
            500,
            evidence.FindProperty(nameof(TripReturnEvidence.ImageUrl))?.GetMaxLength());

        Assert.Contains(
            confirmation.GetCheckConstraints(),
            constraint => constraint.Name == "CK_TripReturnConfirmations_HandoverStatus");
        Assert.Contains(
            evidence.GetCheckConstraints(),
            constraint => constraint.Name == "CK_TripReturnEvidence_DisplayOrder");

        var tripForeignKey = confirmation.GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Trip));
        Assert.Equal(DeleteBehavior.ClientSetNull, tripForeignKey.DeleteBehavior);
        Assert.Equal(
            nameof(Trip.ReturnConfirmations),
            tripForeignKey.PrincipalToDependent?.Name);

        var evidenceForeignKey = evidence.GetForeignKeys()
            .Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(TripReturnConfirmation));
        Assert.Equal(DeleteBehavior.Cascade, evidenceForeignKey.DeleteBehavior);
        Assert.Equal(
            nameof(TripReturnConfirmation.Evidence),
            evidenceForeignKey.PrincipalToDependent?.Name);

        Assert.Contains(
            evidence.GetIndexes(),
            index => index.IsUnique
                && index.Properties.Select(property => property.Name).SequenceEqual(
                    [
                        nameof(TripReturnEvidence.TripReturnConfirmationId),
                        nameof(TripReturnEvidence.DisplayOrder)
                    ]));
    }
}
