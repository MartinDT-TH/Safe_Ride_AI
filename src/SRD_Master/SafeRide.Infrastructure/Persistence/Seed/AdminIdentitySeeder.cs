using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SafeRide.Domain.Entities;

namespace SafeRide.Infrastructure.Persistence;

public static class AdminIdentitySeeder
{
    public static async Task SeedAdminIdentityAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<AspNetRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AspNetUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        const string roleName = "Admin";

        if (!await roles.RoleExistsAsync(roleName))
        {
            var roleResult = await roles.CreateAsync(new AspNetRole
            {
                Id = Guid.NewGuid(), Name = roleName, Description = "SafeRide system administrator"
            });
            if (!roleResult.Succeeded) throw new InvalidOperationException(string.Join("; ", roleResult.Errors.Select(x => x.Description)));
        }

        var email = configuration["AdminSeed:Email"];
        var password = configuration["AdminSeed:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;

        var admin = await users.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new AspNetUser
            {
                Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true,
                FullName = "SafeRide Administrator", IsActive = true, CreatedAt = DateTime.UtcNow
            };
            var createResult = await users.CreateAsync(admin, password);
            if (!createResult.Succeeded) throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(x => x.Description)));
        }
        else if (!await users.CheckPasswordAsync(admin, password))
        {
            if (await users.HasPasswordAsync(admin))
            {
                var removePasswordResult = await users.RemovePasswordAsync(admin);
                if (!removePasswordResult.Succeeded) throw new InvalidOperationException(string.Join("; ", removePasswordResult.Errors.Select(x => x.Description)));
            }
            var addPasswordResult = await users.AddPasswordAsync(admin, password);
            if (!addPasswordResult.Succeeded) throw new InvalidOperationException(string.Join("; ", addPasswordResult.Errors.Select(x => x.Description)));
        }
        if (!await users.IsInRoleAsync(admin, roleName))
        {
            var addRoleResult = await users.AddToRoleAsync(admin, roleName);
            if (!addRoleResult.Succeeded) throw new InvalidOperationException(string.Join("; ", addRoleResult.Errors.Select(x => x.Description)));
        }
    }
}
