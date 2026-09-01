using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata;
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
            table.HasCheckConstraint(
                "CK_Bookings_PricingSnapshotVersion",
                "[PricingSnapshotVersion] IS NULL OR [PricingSnapshotVersion] IN (0, 1)");
            table.HasCheckConstraint(
                "CK_Bookings_PricingSnapshotAmounts",
                "[PricingSnapshotVersion] IS NULL OR [PricingSnapshotVersion] = 0 OR (" +
                "[EstimatedDistanceKm] IS NOT NULL AND [EstimatedDurationMinutes] IS NOT NULL AND " +
                "[SurgeEvaluationTime] IS NOT NULL AND " +
                "[AcceptedBaseFare] IS NOT NULL AND [AcceptedBaseFare] >= 0 AND " +
                "[AcceptedMinimumServiceFare] IS NOT NULL AND [AcceptedMinimumServiceFare] >= 0 AND " +
                "[AcceptedSurgeMultiplier] IS NOT NULL AND [AcceptedSurgeMultiplier] >= 1 AND " +
                "[NormalFare] IS NOT NULL AND [NormalFare] >= 0 AND " +
                "[SurgedFare] IS NOT NULL AND [SurgedFare] >= [NormalFare] AND " +
                "[SurgeAmount] IS NOT NULL AND " +
                "[SurgeAmount] = [SurgedFare] - [NormalFare] AND " +
                "[AcceptedLongDistanceThresholdKm] IS NOT NULL AND [AcceptedLongDistanceThresholdKm] > 0 AND " +
                "[AcceptedLongDistanceOptInThresholdKm] IS NOT NULL AND " +
                "[AcceptedLongDistanceOptInThresholdKm] >= [AcceptedLongDistanceThresholdKm] AND " +
                "[AcceptedMaximumTripDistanceKm] IS NOT NULL AND " +
                "[AcceptedMaximumTripDistanceKm] >= [AcceptedLongDistanceOptInThresholdKm] AND " +
                "[AcceptedLongDistanceRatePerKm] IS NOT NULL AND [AcceptedLongDistanceRatePerKm] >= 0 AND " +
                "[LongDistanceComponent] IS NOT NULL AND [LongDistanceComponent] >= 0 AND " +
                "[NormalFare] = ROUND([NormalFare], 0) AND " +
                "[SurgedFare] = ROUND([SurgedFare], 0) AND " +
                "[SurgeAmount] = ROUND([SurgeAmount], 0) AND " +
                "[LongDistanceComponent] = ROUND([LongDistanceComponent], 0) AND " +
                "[EstimatedFare] = ROUND([EstimatedFare], 0) AND " +
                "[EstimatedFare] = [SurgedFare] + [LongDistanceComponent] AND " +
                "(([AcceptedPricePerKm] IS NOT NULL AND [AcceptedPricePerKm] > 0 AND " +
                "[AcceptedPricePerHour] IS NULL AND NULLIF(LTRIM(RTRIM([RoutePolyline])), '') IS NOT NULL) OR " +
                "([AcceptedPricePerHour] IS NOT NULL AND [AcceptedPricePerHour] > 0 AND " +
                "[AcceptedPricePerKm] IS NULL AND [LongDistanceComponent] = 0)))");
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
            .HasConversion(
                scheduledAt => NormalizeUtc(scheduledAt),
                storedScheduledAt => MarkAsUtc(storedScheduledAt))
            .IsRequired(false);
        builder.Property(booking => booking.EstimatedFare)
            .HasColumnType("decimal(18,2)");
        builder.Property(booking => booking.EstimatedDistanceKm)
            .HasColumnType("decimal(18,6)");
        builder.Property(booking => booking.PricingSnapshotVersion);
        builder.Property(booking => booking.AcceptedBaseFare)
            .HasColumnType("decimal(18,2)");
        builder.Property(booking => booking.AcceptedMinimumServiceFare)
            .HasColumnType("decimal(18,2)");
        builder.Property(booking => booking.AcceptedPricePerKm)
            .HasColumnType("decimal(18,2)");
        builder.Property(booking => booking.AcceptedPricePerHour)
            .HasColumnType("decimal(18,2)");
        builder.Property(booking => booking.AcceptedSurgeMultiplier)
            .HasColumnType("decimal(5,2)");
        builder.Property(booking => booking.SurgeEvaluationTime)
            .HasConversion(
                value => NormalizeUtc(value),
                storedValue => MarkAsUtc(storedValue))
            .IsRequired(false);
        builder.Property(booking => booking.NormalFare)
            .HasColumnType("decimal(18,2)");
        builder.Property(booking => booking.SurgedFare)
            .HasColumnType("decimal(18,2)");
        builder.Property(booking => booking.SurgeAmount)
            .HasColumnType("decimal(18,2)");
        builder.Property(booking => booking.AcceptedLongDistanceThresholdKm)
            .HasColumnType("decimal(18,6)");
        builder.Property(booking => booking.AcceptedLongDistanceOptInThresholdKm)
            .HasColumnType("decimal(18,6)");
        builder.Property(booking => booking.AcceptedMaximumTripDistanceKm)
            .HasColumnType("decimal(18,6)");
        builder.Property(booking => booking.AcceptedLongDistanceRatePerKm)
            .HasColumnType("decimal(18,2)");
        builder.Property(booking => booking.LongDistanceComponent)
            .HasColumnType("decimal(18,2)");
        builder.Property(booking => booking.RoutePolyline)
            .HasColumnType("nvarchar(max)");
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

        ConfigurePricingSnapshotImmutability(builder);
    }

    private static void ConfigurePricingSnapshotImmutability(
        EntityTypeBuilder<Booking> builder)
    {
        string[] propertyNames =
        [
            nameof(Booking.BookingType),
            nameof(Booking.ScheduledAt),
            nameof(Booking.EstimatedDistanceKm),
            nameof(Booking.EstimatedDurationMinutes),
            nameof(Booking.EstimatedFare),
            nameof(Booking.RoutePolyline),
            nameof(Booking.PricingRuleId),
            nameof(Booking.SurgePricingRuleId),
            nameof(Booking.PricingSnapshotVersion),
            nameof(Booking.AcceptedBaseFare),
            nameof(Booking.AcceptedMinimumServiceFare),
            nameof(Booking.AcceptedPricePerKm),
            nameof(Booking.AcceptedPricePerHour),
            nameof(Booking.AcceptedSurgeMultiplier),
            nameof(Booking.SurgeEvaluationTime),
            nameof(Booking.NormalFare),
            nameof(Booking.SurgedFare),
            nameof(Booking.SurgeAmount),
            nameof(Booking.AcceptedLongDistanceThresholdKm),
            nameof(Booking.AcceptedLongDistanceOptInThresholdKm),
            nameof(Booking.AcceptedMaximumTripDistanceKm),
            nameof(Booking.AcceptedLongDistanceRatePerKm),
            nameof(Booking.LongDistanceComponent)
        ];

        foreach (var propertyName in propertyNames)
        {
            builder.Property(propertyName).Metadata.SetAfterSaveBehavior(
                PropertySaveBehavior.Throw);
        }
    }

    private static DateTime? NormalizeUtc(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private static DateTime? MarkAsUtc(DateTime? value)
    {
        return value.HasValue
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : null;
    }
}
