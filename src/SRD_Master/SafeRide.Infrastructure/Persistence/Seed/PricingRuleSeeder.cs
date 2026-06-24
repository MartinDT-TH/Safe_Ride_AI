using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Infrastructure.Persistence;

public static class PricingRuleSeeder
{
    public static async Task SeedPricingAndSurgeRulesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var perTripServiceType = await SeedServiceTypeAsync(dbContext, "PerTrip", cancellationToken);
        var hourlyServiceType = await SeedServiceTypeAsync(dbContext, "Hourly", cancellationToken);

        await SeedPricingRulesAsync(dbContext, cancellationToken);
        await SeedSurgePricingRulesAsync(dbContext, cancellationToken);

        await SeedPromotionAsync(dbContext, DateTime.UtcNow, cancellationToken);
    }

    private static async Task SeedPricingRulesAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.PricingRules.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var rules = new List<PricingRule>
        {
            new() { Id = 1, VehicleClass = RequiredLicenseClass.B, ServiceTypeId = 1, BaseFare = 30000m, MinFare = 70000m, PricePerKm = 12000m, PricePerHour = null, IsActive = true, CreatedAt = now, UpdatedAt = now },
            new() { Id = 2, VehicleClass = RequiredLicenseClass.B, ServiceTypeId = 2, BaseFare = 50000m, MinFare = 100000m, PricePerKm = null, PricePerHour = 90000m, IsActive = true, CreatedAt = now, UpdatedAt = now },
            new() { Id = 6, VehicleClass = RequiredLicenseClass.A1, ServiceTypeId = 1, BaseFare = 15000m, MinFare = 30000m, PricePerKm = 7000m, PricePerHour = null, IsActive = true, CreatedAt = now },
            new() { Id = 7, VehicleClass = RequiredLicenseClass.A1, ServiceTypeId = 2, BaseFare = 20000m, MinFare = 60000m, PricePerKm = null, PricePerHour = 80000m, IsActive = true, CreatedAt = now },
            new() { Id = 9, VehicleClass = RequiredLicenseClass.A, ServiceTypeId = 1, BaseFare = 18000m, MinFare = 35000m, PricePerKm = 8000m, PricePerHour = null, IsActive = true, CreatedAt = now },
            new() { Id = 10, VehicleClass = RequiredLicenseClass.A, ServiceTypeId = 2, BaseFare = 25000m, MinFare = 70000m, PricePerKm = null, PricePerHour = 95000m, IsActive = true, CreatedAt = now }
        };

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        
        if (dbContext.Database.IsSqlServer())
        {
            await dbContext.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[PricingRules] ON", cancellationToken);
        }
        
        dbContext.PricingRules.AddRange(rules);
        await dbContext.SaveChangesAsync(cancellationToken);
        
        if (dbContext.Database.IsSqlServer())
        {
            await dbContext.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [dbo].[PricingRules] OFF", cancellationToken);
        }
        
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task SeedSurgePricingRulesAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        if (await dbContext.SurgePricingRules.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var surgeRules = new List<SurgePricingRule>
        {
            new() { RuleName = "Morning Peak", StartTime = new TimeOnly(7, 0), EndTime = new TimeOnly(9, 0), AppliedDays = "Mon,Tue,Wed,Thu,Fri", SurgeMultiplier = 1.15m, IsActive = true, CreatedAt = now },
            new() { RuleName = "Evening Peak", StartTime = new TimeOnly(17, 0), EndTime = new TimeOnly(20, 0), AppliedDays = "Mon,Tue,Wed,Thu,Fri", SurgeMultiplier = 1.20m, IsActive = true, CreatedAt = now },
            new() { RuleName = "Late Night", StartTime = new TimeOnly(22, 0), EndTime = new TimeOnly(5, 0), AppliedDays = "Mon,Tue,Wed,Thu,Fri,Sat,Sun", SurgeMultiplier = 1.30m, IsActive = true, CreatedAt = now },
            new() { RuleName = "Weekend Evening", StartTime = new TimeOnly(18, 0), EndTime = new TimeOnly(23, 59, 59), AppliedDays = "Sat,Sun", SurgeMultiplier = 1.15m, IsActive = true, CreatedAt = now },
            new() { RuleName = "Rainy Weather", StartTime = new TimeOnly(0, 0), EndTime = new TimeOnly(23, 59, 59), AppliedDays = "Mon,Tue,Wed,Thu,Fri,Sat,Sun", SurgeMultiplier = 1.20m, IsActive = false, CreatedAt = now },
            new() { RuleName = "Holiday / Event", StartTime = new TimeOnly(0, 0), EndTime = new TimeOnly(23, 59, 59), AppliedDays = "Mon,Tue,Wed,Thu,Fri,Sat,Sun", SurgeMultiplier = 1.50m, IsActive = false, CreatedAt = now }
        };

        dbContext.SurgePricingRules.AddRange(surgeRules);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<ServiceType> SeedServiceTypeAsync(
        ApplicationDbContext dbContext,
        string serviceName,
        CancellationToken cancellationToken)
    {
        var serviceType = await dbContext.ServiceTypes
            .FirstOrDefaultAsync(x => x.ServiceName == serviceName, cancellationToken);

        if (serviceType is not null)
        {
            return serviceType;
        }

        serviceType = new ServiceType
        {
            ServiceName = serviceName
        };

        dbContext.ServiceTypes.Add(serviceType);
        await dbContext.SaveChangesAsync(cancellationToken);

        return serviceType;
    }

    private static async Task SeedPromotionAsync(
        ApplicationDbContext dbContext,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.Promotions
            .AnyAsync(x => x.PromotionCode == "SAFERIDE20", cancellationToken);

        if (exists)
        {
            return;
        }

        dbContext.Promotions.Add(new Promotion
        {
            PromotionCode = "SAFERIDE20",
            DiscountType = DiscountType.Percentage,
            DiscountValue = 20,
            StartDate = now,
            EndDate = now.AddDays(30),
            MaxUsageCount = 100,
            CurrentUsageCount = 0,
            MinimumOrderValue = 50000,
            MaximumDiscountValue = 30000,
            UsageLimitPerUser = 1,
            IsActive = true
        });
    }
}
