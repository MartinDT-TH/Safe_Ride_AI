using Microsoft.EntityFrameworkCore;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Persistence.Configurations;

namespace SafeRide.Infrastructure.Persistence;

public partial class ApplicationDbContext
{
    public virtual DbSet<AccountBanConfiguration> AccountBanConfigurations { get; set; }

    public virtual DbSet<AccountBanHistory> AccountBanHistories { get; set; }

    public virtual DbSet<AdminNotification> AdminNotifications { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AccountBanConfigurationConfiguration());
        modelBuilder.ApplyConfiguration(new AccountBanHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new AdminNotificationConfiguration());
    }
}
