using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Infrastructure.Persistence.Configurations;

public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings", table =>
        {
            table.HasCheckConstraint(
                "CK_Bookings_BookingType",
                "[BookingType] IN ('Now', 'Scheduled')");
            table.HasCheckConstraint(
                "CK_Bookings_BookingStatus",
                "[BookingStatus] IN ('PendingSchedule', 'Searching', 'DriverAssigned', 'Cancelled', 'Expired', 'Completed')");
            table.HasCheckConstraint(
                "CK_Bookings_BookingSource",
                "[BookingSource] IN ('Manual', 'VoiceCommand', 'Scheduled')");
            table.HasCheckConstraint(
                "CK_Bookings_ScheduledAt",
                "([BookingType] = 'Now' AND [ScheduledAt] IS NULL) OR ([BookingType] = 'Scheduled' AND [ScheduledAt] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Bookings_PickupLocation",
                "[PickupLocation].STSrid = 4326");
            table.HasCheckConstraint(
                "CK_Bookings_DestinationLocation",
                "[DestinationLocation] IS NULL OR [DestinationLocation].STSrid = 4326");
            table.HasCheckConstraint(
                "CK_Bookings_EstimatedDistanceKm",
                "[EstimatedDistanceKm] IS NULL OR [EstimatedDistanceKm] >= 0");
            table.HasCheckConstraint(
                "CK_Bookings_EstimatedDurationMinutes",
                "[EstimatedDurationMinutes] IS NULL OR [EstimatedDurationMinutes] >= 0");
            table.HasCheckConstraint(
                "CK_Bookings_EstimatedFare",
                "[EstimatedFare] >= 0");
        });

        builder.HasKey(booking => booking.BookingId)
            .HasName("PK__Bookings__3214EC073A8063CD");
        builder.Property(booking => booking.BookingId)
            .HasColumnName("Id");

        builder.Property(booking => booking.BookingType)
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(booking => booking.BookingStatus)
            .HasConversion<string>()
            .HasMaxLength(30);
        builder.Property(booking => booking.BookingSource)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(BookingSource.Manual);
        builder.Property(booking => booking.ScheduledAt)
            .IsRequired(false);
        builder.Property(booking => booking.EstimatedFare)
            .HasColumnType("decimal(18,2)");
        builder.Property(booking => booking.EstimatedDistanceKm)
            .HasColumnType("decimal(10,2)");
        builder.Property(booking => booking.DestinationLocation)
            .IsRequired(false);
        builder.Property(booking => booking.PickupAddress)
            .HasMaxLength(255);
        builder.Property(booking => booking.DestinationAddress)
            .HasMaxLength(255);
        builder.Property(booking => booking.SpecialRequest)
            .HasMaxLength(500);
        builder.Property(booking => booking.CancellationReason)
            .HasMaxLength(255);

        builder.HasIndex(booking => booking.CustomerId);
        builder.HasIndex(booking => booking.VehicleId);
        builder.HasIndex(booking => booking.BookingStatus);
        builder.HasIndex(booking => booking.BookingType);
        builder.HasIndex(booking => booking.ScheduledAt);

        builder.HasOne(booking => booking.CancelledByNavigation)
            .WithMany(user => user.BookingCancelledByNavigations)
            .HasForeignKey(booking => booking.CancelledBy)
            .HasConstraintName("FK_Bookings_CancelledBy");
        builder.HasOne(booking => booking.Customer)
            .WithMany(user => user.BookingCustomers)
            .HasForeignKey(booking => booking.CustomerId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Bookings_AspNetUsers");
        builder.HasOne(booking => booking.PricingRule)
            .WithMany(rule => rule.Bookings)
            .HasForeignKey(booking => booking.PricingRuleId)
            .HasConstraintName("FK_Bookings_PricingRule");
        builder.HasOne(booking => booking.ServiceType)
            .WithMany(serviceType => serviceType.Bookings)
            .HasForeignKey(booking => booking.ServiceTypeId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Booking_ServiceType");
        builder.HasOne(booking => booking.SurgePricingRule)
            .WithMany(rule => rule.Bookings)
            .HasForeignKey(booking => booking.SurgePricingRuleId)
            .HasConstraintName("FK_Bookings_SurgeRule");
        builder.HasOne(booking => booking.Vehicle)
            .WithMany(vehicle => vehicle.Bookings)
            .HasForeignKey(booking => booking.VehicleId)
            .OnDelete(DeleteBehavior.ClientSetNull)
            .HasConstraintName("FK_Booking_Vehicle");
    }
}
