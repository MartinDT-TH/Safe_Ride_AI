using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafeRide.Domain.Entities;

namespace SafeRide.Infrastructure.Persistence.Configurations;

public sealed class CustomerBehaviorEventConfiguration : IEntityTypeConfiguration<CustomerBehaviorEvent>
{
    public void Configure(EntityTypeBuilder<CustomerBehaviorEvent> builder)
    {
        builder.HasKey(x => x.Id).HasName("PK_CustomerBehaviorEvents");
        builder.ToTable("CustomerBehaviorEvents");
        builder.Property(x => x.EventType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ArrivalLatitude).HasColumnType("decimal(9, 6)");
        builder.Property(x => x.ArrivalLongitude).HasColumnType("decimal(9, 6)");
        builder.Property(x => x.ArrivalDistanceMeters).HasColumnType("decimal(18, 3)");
        builder.Property(x => x.ReviewReason).HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("(getutcdate())");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("(getutcdate())");
        builder.HasIndex(x => new { x.TripId, x.EventType }).IsUnique();
        builder.HasIndex(x => x.CustomerId);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Trip).WithMany().HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Driver).WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReviewedByUser).WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
