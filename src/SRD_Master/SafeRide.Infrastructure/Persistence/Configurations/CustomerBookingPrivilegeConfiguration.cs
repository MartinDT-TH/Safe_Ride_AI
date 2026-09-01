using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SafeRide.Domain.Entities;

namespace SafeRide.Infrastructure.Persistence.Configurations;

public sealed class CustomerBookingPrivilegeConfiguration : IEntityTypeConfiguration<CustomerBookingPrivilege>
{
    public void Configure(EntityTypeBuilder<CustomerBookingPrivilege> builder)
    {
        builder.HasKey(x => x.CustomerId).HasName("PK_CustomerBookingPrivileges");
        builder.ToTable("CustomerBookingPrivileges");
        builder.Property(x => x.NoShowRate).HasColumnType("decimal(9, 6)");
        builder.Property(x => x.RestrictionLevel).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("(getutcdate())");
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Cascade);
    }
}
