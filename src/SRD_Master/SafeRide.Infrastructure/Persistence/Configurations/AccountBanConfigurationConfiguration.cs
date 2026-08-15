using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafeRide.Domain.Entities;

namespace SafeRide.Infrastructure.Persistence.Configurations;

public sealed class AccountBanConfigurationConfiguration
    : IEntityTypeConfiguration<AccountBanConfiguration>
{
    public void Configure(EntityTypeBuilder<AccountBanConfiguration> builder)
    {
        builder.HasKey(x => x.Id).HasName("PK_AccountBanConfigurations");

        builder.ToTable("AccountBanConfigurations", table =>
        {
            table.HasCheckConstraint(
                "CK_AccountBanConfigurations_Singleton",
                $"[Id] = {AccountBanConfiguration.SingletonId}");
            table.HasCheckConstraint(
                "CK_AccountBanConfigurations_NegativeFeedbackThreshold",
                "[NegativeFeedbackThreshold] > 0");
            table.HasCheckConstraint(
                "CK_AccountBanConfigurations_NegativeRatingMaxScore",
                "[NegativeRatingMaxScore] BETWEEN 1 AND 5");
            table.HasCheckConstraint(
                "CK_AccountBanConfigurations_TemporaryBanDurationDays",
                "[TemporaryBanDurationDays] > 0");
            table.HasCheckConstraint(
                "CK_AccountBanConfigurations_MaximumTemporaryBans",
                "[MaximumTemporaryBans] > 0");
        });

        builder.Property(x => x.Id)
            .ValueGeneratedNever();
        builder.Property(x => x.NegativeFeedbackThreshold);
        builder.Property(x => x.NegativeRatingMaxScore);
        builder.Property(x => x.TemporaryBanDurationDays);
        builder.Property(x => x.MaximumTemporaryBans);
        builder.Property(x => x.IsEnabled)
            .HasDefaultValue(true);
        builder.Property(x => x.CreatedAt)
            .HasDefaultValueSql("(getutcdate())");
        builder.Property(x => x.UpdatedAt)
            .HasDefaultValueSql("(getutcdate())");

        builder.HasOne(x => x.UpdatedByUser)
            .WithMany()
            .HasForeignKey(x => x.UpdatedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("FK_AccountBanConfigurations_UpdatedByUser");

        builder.HasData(new AccountBanConfiguration
        {
            Id = AccountBanConfiguration.SingletonId,
            NegativeFeedbackThreshold = 5,
            NegativeRatingMaxScore = 2,
            TemporaryBanDurationDays = 15,
            MaximumTemporaryBans = 3,
            IsEnabled = true,
            CreatedAt = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
