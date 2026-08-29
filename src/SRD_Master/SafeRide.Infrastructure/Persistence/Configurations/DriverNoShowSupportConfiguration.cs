using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafeRide.Domain.Entities;

namespace SafeRide.Infrastructure.Persistence.Configurations;

public sealed class DriverNoShowSupportConfiguration : IEntityTypeConfiguration<DriverNoShowSupport>
{
    public void Configure(EntityTypeBuilder<DriverNoShowSupport> builder)
    {
        builder.HasKey(x => x.Id).HasName("PK_DriverNoShowSupports");
        builder.ToTable("DriverNoShowSupports");
        builder.Property(x => x.AcceptedPickupDistanceKm).HasColumnType("decimal(18, 3)");
        builder.Property(x => x.SupportAmount).HasColumnType("decimal(18, 2)");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("(getutcdate())");
        builder.HasIndex(x => x.TripId).IsUnique();
        builder.HasOne(x => x.Trip).WithMany().HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CustomerBehaviorEvent).WithMany().HasForeignKey(x => x.CustomerBehaviorEventId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.WalletTransaction).WithMany().HasForeignKey(x => x.WalletTransactionId).OnDelete(DeleteBehavior.Restrict);
    }
}
