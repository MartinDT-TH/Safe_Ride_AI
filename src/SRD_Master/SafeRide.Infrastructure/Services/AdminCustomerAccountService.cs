using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.AdminCustomers;
using SafeRide.Application.Features.AdminCustomers.Queries.GetAdminCustomers;
using SafeRide.Application.Features.AdminUserAccounts;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Persistence;
using System.Security.Claims;

namespace SafeRide.Infrastructure.Services;

public sealed class AdminCustomerAccountService : IAdminCustomerAccountService
{
    private const string CustomerRole = "Customer";
    private const string DriverRole = "Driver";

    private readonly ApplicationDbContext _db;
    private readonly UserManager<AspNetUser> _userManager;
    private readonly IAccountRestrictionService _accountRestrictionService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AdminCustomerAccountService(
        ApplicationDbContext db,
        UserManager<AspNetUser> userManager,
        IAccountRestrictionService accountRestrictionService,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _userManager = userManager;
        _accountRestrictionService = accountRestrictionService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<GetAdminCustomersResult> GetCustomersAsync(CancellationToken cancellationToken)
    {
        var customerUsers = await _userManager.GetUsersInRoleAsync(CustomerRole);
        var driverUsers = await _userManager.GetUsersInRoleAsync(DriverRole);
        var pendingDriverIds = await _db.DriverKycs
            .AsNoTracking()
            .Select(x => x.DriverId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var excludedIds = driverUsers
            .Select(x => x.Id)
            .Concat(pendingDriverIds)
            .Distinct()
            .ToHashSet();
        var customerIds = customerUsers
            .Select(x => x.Id)
            .Where(id => !excludedIds.Contains(id))
            .Distinct()
            .ToArray();
        var customers = await LoadCustomersAsync(customerIds, cancellationToken);
        var counts = new AdminCustomerCountsResponse(
            customers.Count,
            customers.Count(x => x.Status == "active"),
            customers.Count(x => x.Status == "blocked"),
            customers.Count(x => x.Tier == "premium"));

        return new GetAdminCustomersResult(customers, counts);
    }

    public async Task<AdminCustomerResponse> BlockCustomerAsync(
        Guid customerId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var user = await FindCustomerAsync(customerId, cancellationToken)
            ?? throw new AdminUserAccountException(
                "admin.customer.not_found",
                "Không tìm thấy khách hàng.",
                StatusCodes.Status404NotFound);

        user.IsActive = false;
        user.BanReason = string.IsNullOrWhiteSpace(reason)
            ? "Khóa bởi quản trị viên"
            : reason.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        EnsureIdentitySucceeded(
            result,
            "admin.customer.block_failed",
            "Không thể khóa khách hàng.");
        if (TryGetAdminUserId(out var adminUserId))
        {
            await _accountRestrictionService.RecordManualLockAsync(
                customerId,
                adminUserId,
                user.BanReason,
                cancellationToken);
        }

        return (await LoadCustomersAsync([customerId], cancellationToken)).Single();
    }

    public async Task<AdminCustomerResponse> UnlockCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var user = await FindCustomerAsync(customerId, cancellationToken)
            ?? throw new AdminUserAccountException(
                "admin.customer.not_found",
                "Không tìm thấy khách hàng.",
                StatusCodes.Status404NotFound);

        user.IsActive = true;
        user.BanReason = null;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        EnsureIdentitySucceeded(
            result,
            "admin.customer.unlock_failed",
            "Không thể mở khóa khách hàng.");
        if (TryGetAdminUserId(out var adminUserId))
        {
            await _accountRestrictionService.RecordManualUnlockAsync(
                customerId,
                adminUserId,
                cancellationToken);
        }
        var restriction = await _accountRestrictionService.CheckAccountAccessAsync(
            customerId,
            releaseExpiredTemporaryBans: false,
            cancellationToken);
        if (!restriction.IsAllowed)
        {
            user.IsActive = false;
            user.BanReason = restriction.Message;
            user.UpdatedAt = DateTime.UtcNow;
            EnsureIdentitySucceeded(
                await _userManager.UpdateAsync(user),
                "admin.customer.unlock_restricted",
                "Khách hàng vẫn đang bị giới hạn bởi quy tắc khác.");
        }

        return (await LoadCustomersAsync([customerId], cancellationToken)).Single();
    }

    private async Task<AspNetUser?> FindCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(customerId.ToString());
        if (user is null)
        {
            return null;
        }

        if (!await _userManager.IsInRoleAsync(user, CustomerRole))
        {
            return null;
        }

        if (await _userManager.IsInRoleAsync(user, DriverRole))
        {
            return null;
        }

        var hasDriverRegistration = await _db.DriverProfiles
            .AsNoTracking()
            .AnyAsync(x => x.DriverId == customerId, cancellationToken)
            || await _db.DriverKycs
                .AsNoTracking()
                .AnyAsync(x => x.DriverId == customerId, cancellationToken);

        return hasDriverRegistration ? null : user;
    }

    private async Task<List<AdminCustomerResponse>> LoadCustomersAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken)
    {
        var idArray = ids.ToArray();
        if (idArray.Length == 0)
        {
            return [];
        }

        var users = await _db.Users
            .AsNoTracking()
            .Where(x => idArray.Contains(x.Id))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return users.Select(AdminCustomerResponse.From).ToList();
    }

    private static void EnsureIdentitySucceeded(
        IdentityResult result,
        string code,
        string fallbackMessage)
    {
        if (result.Succeeded)
        {
            return;
        }

        var message = result.Errors.Any()
            ? string.Join("; ", result.Errors.Select(x => x.Description))
            : fallbackMessage;

        throw new AdminUserAccountException(
            code,
            message,
            StatusCodes.Status400BadRequest);
    }

    private bool TryGetAdminUserId(out Guid adminUserId)
    {
        return Guid.TryParse(
            _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),
            out adminUserId);
    }
}
