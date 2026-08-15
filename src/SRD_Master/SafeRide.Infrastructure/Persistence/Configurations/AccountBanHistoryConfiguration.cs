using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Infrastructure.Persistence.Configurations;

public sealed class AccountBanHistoryConfiguration
    : IEntityTypeConfiguration<AccountBanHistory>
{
    public void Configure(EntityTypeBuilder<AccountBanHistory> builder)
    {
        builder.HasKey(x => x.Id).HasName("PK_AccountBanHistories");

        builder.ToTable("AccountBanHistories", table =>
        {
            table.HasCheckConstraint(
                "CK_AccountBanHistories_EndAfterStart",
                "[EndsAt] IS NULL OR [EndsAt] > [StartedAt]");
            table.HasCheckConstraint(
                "CK_AccountBanHistories_NegativeFeedbackCount",
                "[NegativeFeedbackCount] IS NULL OR [NegativeFeedbackCount] >= 0");
            table.HasCheckConstraint(
                "CK_AccountBanHistories_TemporaryBanSequence",
                "[TemporaryBanSequence] IS NULL OR [TemporaryBanSequence] > 0");
        });

        builder.Property(x => x.BanType)
            .HasConversion<string>()
            .HasMaxLength(30);
        builder.Property(x => x.Source)
            .HasConversion<string>()
            .HasMaxLength(40);
        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(AccountBanStatus.Active);
        builder.Property(x => x.Reason)
            .HasMaxLength(500);
        builder.Property(x => x.Trigger)
            .HasMaxLength(100);
        builder.Property(x => x.ReleaseReason)
            .HasMaxLength(500);
        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("(getutcdate())");

        builder.HasIndex(x => new { x.UserId, x.Status });
        builder.HasIndex(x => new { x.UserId, x.Source, x.BanType, x.CreatedAt });
        builder.HasIndex(x => x.TriggeringRatingId);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_AccountBanHistories_User");

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AccountBanHistories_CreatedByUser");

        builder.HasOne(x => x.ReleasedByUser)
            .WithMany()
            .HasForeignKey(x => x.ReleasedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_AccountBanHistories_ReleasedByUser");

        builder.HasOne(x => x.TriggeringRating)
            .WithMany()
            .HasForeignKey(x => x.TriggeringRatingId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_AccountBanHistories_TriggeringRating");
    }
}
