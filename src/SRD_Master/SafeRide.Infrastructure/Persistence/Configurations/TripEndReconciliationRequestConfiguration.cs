using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Infrastructure.Persistence.Configurations;

public sealed class TripEndReconciliationRequestConfiguration
    : IEntityTypeConfiguration<TripEndReconciliationRequest>
{
    public void Configure(EntityTypeBuilder<TripEndReconciliationRequest> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestedReason).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ResolutionNote).HasMaxLength(1000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => x.TripId)
            .IsUnique()
            .HasFilter("[Status] = 'PENDING'")
            .HasDatabaseName("UX_TripEndReconciliations_Trip_Pending");
        builder.ToTable("TripEndReconciliationRequests", table =>
        {
            table.HasCheckConstraint(
                "CK_TripEndReconciliations_Reason",
                "[RequestedReason] IN ('DRIVER_UNABLE_TO_CONTINUE','STARTED_BY_MISTAKE')");
            table.HasCheckConstraint(
                "CK_TripEndReconciliations_Status",
                "[Status] IN ('PENDING','APPROVED','REJECTED')");
        });
        builder.HasOne(x => x.Trip).WithMany(x => x.EndReconciliationRequests)
            .HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RequestedByDriver).WithMany()
            .HasForeignKey(x => x.RequestedByDriverId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ResolvedByStaff).WithMany()
            .HasForeignKey(x => x.ResolvedByStaffId).OnDelete(DeleteBehavior.Restrict);
    }
}
