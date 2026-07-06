using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafeRide.Domain.Entities;

namespace SafeRide.Infrastructure.Persistence.Configurations;

public sealed class TripReturnConfirmationConfiguration
    : IEntityTypeConfiguration<TripReturnConfirmation>
{
    public void Configure(EntityTypeBuilder<TripReturnConfirmation> builder)
    {
        builder.HasKey(confirmation => confirmation.Id)
            .HasName("PK_TripReturnConfirmations");

        builder.ToTable("TripReturnConfirmations", table =>
        {
            table.HasCheckConstraint(
                "CK_TripReturnConfirmations_HandoverStatus",
                "[HandoverStatus] IN ('Pending', 'CustomerConfirmed', 'DriverConfirmed', 'Disputed', 'Resolved')");
            table.HasCheckConstraint(
                "CK_TripReturnConfirmations_DriverLatitude",
                "[DriverLatitude] IS NULL OR ([DriverLatitude] >= -90 AND [DriverLatitude] <= 90)");
            table.HasCheckConstraint(
                "CK_TripReturnConfirmations_DriverLongitude",
                "[DriverLongitude] IS NULL OR ([DriverLongitude] >= -180 AND [DriverLongitude] <= 180)");
        });

        builder.Property(confirmation => confirmation.HandoverStatus)
            .HasConversion<string>()
            .HasMaxLength(30);
        builder.Property(confirmation => confirmation.ConfirmedAt)
            .HasDefaultValueSql("(getutcdate())");
        builder.Property(confirmation => confirmation.CreatedAt)
            .HasDefaultValueSql("(getutcdate())");
        builder.Property(confirmation => confirmation.DriverLatitude)
            .HasColumnType("decimal(9, 6)");
        builder.Property(confirmation => confirmation.DriverLongitude)
            .HasColumnType("decimal(9, 6)");
        builder.Property(confirmation => confirmation.Note)
            .HasMaxLength(1000);

        builder.HasIndex(confirmation => confirmation.TripId);
        builder.HasIndex(confirmation => confirmation.DriverId);
        builder.HasIndex(confirmation => confirmation.ConfirmedByUserId);
        builder.HasIndex(confirmation => new
        {
            confirmation.TripId,
            confirmation.HandoverStatus
        });

        builder.HasOne(confirmation => confirmation.Trip)
            .WithMany(trip => trip.ReturnConfirmations)
            .HasForeignKey(confirmation => confirmation.TripId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_TripReturnConfirmations_Trips");

        builder.HasOne(confirmation => confirmation.Driver)
            .WithMany()
            .HasForeignKey(confirmation => confirmation.DriverId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_TripReturnConfirmations_DriverProfiles");

        builder.HasOne(confirmation => confirmation.ConfirmedByUser)
            .WithMany()
            .HasForeignKey(confirmation => confirmation.ConfirmedByUserId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_TripReturnConfirmations_AspNetUsers");
    }
}
