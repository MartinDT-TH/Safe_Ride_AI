using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SafeRide.Domain.Entities;

namespace SafeRide.Infrastructure.Persistence;

public static class IdentitySeeder
{
    public static async Task SeedIdentityAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AspNetRole>>();

        if (await roleManager.RoleExistsAsync("Customer"))
        {
            return;
        }

        var result = await roleManager.CreateAsync(new AspNetRole
        {
            Id = Guid.NewGuid(),
            Name = "Customer",
            Description = "Default customer role"
        });

        if (!result.Succeeded && !await roleManager.RoleExistsAsync("Customer"))
        {
            throw new InvalidOperationException(
                $"Could not seed Customer role: {string.Join("; ", result.Errors.Select(x => x.Description))}");
        }
    }
}