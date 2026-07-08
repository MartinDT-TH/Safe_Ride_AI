using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafeRide.Domain.Entities;

namespace SafeRide.Infrastructure.Persistence.Configurations;

public sealed class TripReturnEvidenceConfiguration
    : IEntityTypeConfiguration<TripReturnEvidence>
{
    public void Configure(EntityTypeBuilder<TripReturnEvidence> builder)
    {
        builder.HasKey(evidence => evidence.Id)
            .HasName("PK_TripReturnEvidence");

        builder.ToTable("TripReturnEvidence", table =>
        {
            table.HasCheckConstraint(
                "CK_TripReturnEvidence_DisplayOrder",
                "[DisplayOrder] BETWEEN 1 AND 3");
            table.HasCheckConstraint(
                "CK_TripReturnEvidence_FileSizeBytes",
                "[FileSizeBytes] IS NULL OR [FileSizeBytes] > 0");
        });

        builder.Property(evidence => evidence.ImageUrl)
            .HasMaxLength(500);
        builder.Property(evidence => evidence.ImagePublicId)
            .HasMaxLength(255);
        builder.Property(evidence => evidence.OriginalFileName)
            .HasMaxLength(255);
        builder.Property(evidence => evidence.ContentType)
            .HasMaxLength(100)
            .IsUnicode(false);
        builder.Property(evidence => evidence.CreatedAt)
            .HasDefaultValueSql("(getutcdate())");

        builder.HasIndex(evidence => evidence.TripReturnConfirmationId);
        builder.HasIndex(evidence => new
        {
            evidence.TripReturnConfirmationId,
            evidence.DisplayOrder
        }).IsUnique();

        builder.HasOne(evidence => evidence.TripReturnConfirmation)
            .WithMany(confirmation => confirmation.Evidence)
            .HasForeignKey(evidence => evidence.TripReturnConfirmationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_TripReturnEvidence_TripReturnConfirmations");
    }
}
