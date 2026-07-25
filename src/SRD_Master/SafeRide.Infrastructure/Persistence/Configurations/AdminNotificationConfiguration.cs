using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Infrastructure.Persistence.Configurations;

public sealed class AdminNotificationConfiguration
    : IEntityTypeConfiguration<AdminNotification>
{
    public void Configure(EntityTypeBuilder<AdminNotification> builder)
    {
        builder.HasKey(x => x.Id).HasName("PK_AdminNotifications");

        builder.ToTable("AdminNotifications");

        builder.Property(x => x.Title)
            .HasMaxLength(40);

        builder.Property(x => x.Content)
            .HasMaxLength(140);

        builder.Property(x => x.NotificationType)
            .HasMaxLength(50)
            .IsUnicode(false);

        builder.Property(x => x.TargetAudience)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValueSql($"('{AdminNotificationStatus.Pending}')");

        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("(getutcdate())");

        builder.Property(x => x.RejectedReason)
            .HasMaxLength(255);

        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => x.NotificationType);
        builder.HasIndex(x => x.TargetAudience);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AdminNotifications_CreatedByUser");

        builder.HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AdminNotifications_ApprovedByUser");

        builder.HasOne(x => x.RejectedByUser)
            .WithMany()
            .HasForeignKey(x => x.RejectedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AdminNotifications_RejectedByUser");
    }
}
