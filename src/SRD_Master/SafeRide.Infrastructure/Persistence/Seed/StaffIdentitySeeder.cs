using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SafeRide.Domain.Entities;

namespace SafeRide.Infrastructure.Persistence;

public static class StaffIdentitySeeder
{
    public static async Task SeedStaffIdentityAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<AspNetRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AspNetUser>>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AspNetUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        const string roleName = "Staff";
        var email = configuration["StaffSeed:Email"] ?? "staff@gmail.com";
        var password = configuration["StaffSeed:Password"] ?? "staff@123";
        var phoneNumber = configuration["StaffSeed:PhoneNumber"] ?? "0909000001";
        var fullName = configuration["StaffSeed:FullName"] ?? "SafeRide Staff";

        if (!await roles.RoleExistsAsync(roleName))
        {
            var roleResult = await roles.CreateAsync(new AspNetRole
            {
                Id = Guid.NewGuid(),
                Name = roleName,
                Description = "SafeRide operations staff"
            });
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join("; ", roleResult.Errors.Select(x => x.Description)));
            }
        }

        var staff = await users.FindByEmailAsync(email);
        if (staff is null)
        {
            staff = new AspNetUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                PhoneNumber = phoneNumber,
                PhoneNumberConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            staff.PasswordHash = passwordHasher.HashPassword(staff, password);
            staff.SecurityStamp = Guid.NewGuid().ToString();

            var createResult = await users.CreateAsync(staff);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join("; ", createResult.Errors.Select(x => x.Description)));
            }
        }
        else if (!await users.CheckPasswordAsync(staff, password))
        {
            staff.PasswordHash = passwordHasher.HashPassword(staff, password);
            staff.SecurityStamp = Guid.NewGuid().ToString();
        }

        staff.UserName = email;
        staff.Email = email;
        staff.EmailConfirmed = true;
        staff.FullName = string.IsNullOrWhiteSpace(staff.FullName) ? fullName : staff.FullName;
        staff.PhoneNumber = string.IsNullOrWhiteSpace(staff.PhoneNumber) ? phoneNumber : staff.PhoneNumber;
        staff.PhoneNumberConfirmed = true;
        staff.IsActive = true;
        staff.UpdatedAt = DateTime.UtcNow;

        var updateResult = await users.UpdateAsync(staff);
        if (!updateResult.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", updateResult.Errors.Select(x => x.Description)));
        }

        if (!await users.IsInRoleAsync(staff, roleName))
        {
            var addRoleResult = await users.AddToRoleAsync(staff, roleName);
            if (!addRoleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    string.Join("; ", addRoleResult.Errors.Select(x => x.Description)));
            }
        }
    }
}
