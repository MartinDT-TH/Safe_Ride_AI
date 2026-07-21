using Microsoft.EntityFrameworkCore;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Persistence.Configurations;

namespace SafeRide.Infrastructure.Persistence;

public partial class ApplicationDbContext
{
    public virtual DbSet<AdminNotification> AdminNotifications { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AdminNotificationConfiguration());
    }
}
