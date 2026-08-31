using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.AdminDrivers;
using SafeRide.Application.Features.AdminDrivers.Queries.GetAdminDrivers;
using SafeRide.Application.Features.AdminUserAccounts;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;
using System.Security.Claims;

namespace SafeRide.Infrastructure.Services;

public sealed class AdminDriverAccountService : IAdminDriverAccountService
{
    private const string DriverRole = "Driver";

    private readonly ApplicationDbContext _db;
    private readonly UserManager<AspNetUser> _userManager;
    private readonly IAccountRestrictionService _accountRestrictionService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AdminDriverAccountService(
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

    public async Task<GetAdminDriversResult> GetDriversAsync(
        string status,
        CancellationToken cancellationToken)
    {
        var driverUsers = await _userManager.GetUsersInRoleAsync(DriverRole);
        var pendingApplicantIds = await _db.DriverKycs
            .AsNoTracking()
            .Where(x => x.KycStatus == KycStatus.Pending)
            .Select(x => x.DriverId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var driverIds = driverUsers
            .Select(x => x.Id)
            .Concat(pendingApplicantIds)
            .Distinct()
            .ToArray();
        var drivers = await LoadDriversAsync(driverIds, cancellationToken);
        var counts = new AdminDriverCountsResponse(
            drivers.Count,
            drivers.Count(x => x.Status == "active"),
            drivers.Count(x => x.WorkStatus == DriverWorkStatus.Busy.ToString()),
            drivers.Count(x => x.Status == "pending_kyc"),
            drivers.Count(x => x.Status == "blocked"));
        var filteredDrivers = FilterDrivers(drivers, status);

        return new GetAdminDriversResult(filteredDrivers, counts);
    }

    public async Task<AdminDriverResponse> BlockDriverAsync(
        Guid driverId,
        string? reason,
        CancellationToken cancellationToken)
    {
        var user = await FindDriverAsync(driverId)
            ?? throw new AdminUserAccountException(
                "admin.driver.not_found",
                "Không tìm thấy tài xế.",
                StatusCodes.Status404NotFound);

        user.IsActive = false;
        user.BanReason = string.IsNullOrWhiteSpace(reason)
            ? "Khóa bởi quản trị viên"
            : reason.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        EnsureIdentitySucceeded(
            result,
            "admin.driver.block_failed",
            "Không thể khóa tài xế.");

        if (TryGetAdminUserId(out var adminUserId))
        {
            await _accountRestrictionService.RecordManualLockAsync(
                driverId,
                adminUserId,
                user.BanReason,
                cancellationToken);
        }

        return (await LoadDriversAsync([driverId], cancellationToken)).Single();
    }

    public async Task<AdminDriverResponse> UnlockDriverAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var user = await FindDriverAsync(driverId)
            ?? throw new AdminUserAccountException(
                "admin.driver.not_found",
                "Không tìm thấy tài xế.",
                StatusCodes.Status404NotFound);

        user.IsActive = true;
        user.BanReason = null;
        user.UpdatedAt = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        EnsureIdentitySucceeded(
            result,
            "admin.driver.unlock_failed",
            "Không thể mở khóa tài xế.");
        if (TryGetAdminUserId(out var adminUserId))
        {
            await _accountRestrictionService.RecordManualUnlockAsync(
                driverId,
                adminUserId,
                cancellationToken);
        }
        var restriction = await _accountRestrictionService.CheckAccountAccessAsync(
            driverId,
            releaseExpiredTemporaryBans: false,
            cancellationToken);
        if (!restriction.IsAllowed)
        {
            user.IsActive = false;
            user.BanReason = restriction.Message;
            user.UpdatedAt = DateTime.UtcNow;
            EnsureIdentitySucceeded(
                await _userManager.UpdateAsync(user),
                "admin.driver.unlock_restricted",
                "Tài xế vẫn đang bị giới hạn bởi quy tắc khác.");
        }

        return (await LoadDriversAsync([driverId], cancellationToken)).Single();
    }

    public async Task<AdminDriverResponse> ReviewKycAsync(
        Guid driverId,
        KycStatus status,
        string? rejectionReason,
        CancellationToken cancellationToken)
    {
        if (status is not KycStatus.Approved and not KycStatus.Rejected)
        {
            throw new AdminUserAccountException(
                "admin.driver.kyc_invalid_status",
                "Trạng thái KYC phải là Approved hoặc Rejected.",
                StatusCodes.Status400BadRequest);
        }

        if (status == KycStatus.Rejected && string.IsNullOrWhiteSpace(rejectionReason))
        {
            throw new AdminUserAccountException(
                "admin.driver.kyc_rejection_reason_required",
                "Cần nhập lý do từ chối hồ sơ.",
                StatusCodes.Status400BadRequest);
        }

        var applicant = await _userManager.FindByIdAsync(driverId.ToString())
            ?? throw new AdminUserAccountException(
                "admin.driver.applicant_not_found",
                "Không tìm thấy người nộp hồ sơ.",
                StatusCodes.Status404NotFound);

        var documents = await _db.DriverKycs
            .Where(x => x.DriverId == driverId)
            .ToListAsync(cancellationToken);
        if (documents.Count == 0)
        {
            throw new AdminUserAccountException(
                "admin.driver.kyc_not_found",
                "Tài xế chưa có hồ sơ KYC.",
                StatusCodes.Status404NotFound);
        }

        var now = DateTime.UtcNow;
        foreach (var document in documents.Where(x => x.KycStatus == KycStatus.Pending))
        {
            document.KycStatus = status;
            document.VerifiedAt = now;
            document.RejectionReason = status == KycStatus.Rejected
                ? rejectionReason?.Trim()
                : null;
        }

        if (status == KycStatus.Approved)
        {
            var identityCard = documents.FirstOrDefault(
                x => x.DocumentType == KycDocumentType.ID_CARD);
            if (identityCard is null
                || string.IsNullOrWhiteSpace(identityCard.DocumentNumber))
            {
                throw new AdminUserAccountException(
                    "admin.driver.identity_card_required",
                    "Hồ sơ chưa có số CCCD nên chưa thể phê duyệt tài xế.",
                    StatusCodes.Status400BadRequest);
            }

            var drivingLicenses = documents
                .Where(x => x.DocumentType == KycDocumentType.DRIVING_LICENSE
                    && !string.IsNullOrWhiteSpace(x.DocumentNumber)
                    && x.LicenseClass.HasValue)
                .ToList();
            if (drivingLicenses.Count == 0)
            {
                throw new AdminUserAccountException(
                    "admin.driver.driving_license_required",
                    "Hồ sơ chưa có giấy phép lái xe hợp lệ.",
                    StatusCodes.Status400BadRequest);
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (!drivingLicenses.Any(x =>
                    !x.ExpiryDate.HasValue || x.ExpiryDate.Value >= today))
            {
                throw new AdminUserAccountException(
                    "admin.driver.driving_license_expired",
                    "Giấy phép lái xe đã hết hạn nên không thể phê duyệt.",
                    StatusCodes.Status400BadRequest);
            }

            var identityCardNumber = identityCard.DocumentNumber;
            var profile = await _db.DriverProfiles.FindAsync([driverId], cancellationToken);
            if (profile is null)
            {
                _db.DriverProfiles.Add(new DriverProfile
                {
                    DriverId = driverId,
                    IdentityCardNumber = identityCardNumber,
                    HomeAddress = identityCard.Address,
                    WorkStatus = DriverWorkStatus.Offline,
                    CreatedAt = now
                });
            }
            else
            {
                profile.IdentityCardNumber = identityCardNumber;
                profile.HomeAddress = identityCard.Address ?? profile.HomeAddress;
                profile.UpdatedAt = now;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (status == KycStatus.Approved && !await _userManager.IsInRoleAsync(applicant, DriverRole))
        {
            var roleResult = await _userManager.AddToRoleAsync(applicant, DriverRole);
            EnsureIdentitySucceeded(
                roleResult,
                "admin.driver.role_assign_failed",
                "Không thể gán quyền tài xế.");
        }

        return (await LoadDriversAsync([driverId], cancellationToken)).Single();
    }

    private async Task<AspNetUser?> FindDriverAsync(Guid driverId)
    {
        var user = await _userManager.FindByIdAsync(driverId.ToString());
        return user is not null && await _userManager.IsInRoleAsync(user, DriverRole)
            ? user
            : null;
    }

    private async Task<List<AdminDriverResponse>> LoadDriversAsync(
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
            .Include(x => x.DriverProfile)
                .ThenInclude(x => x!.Ratings)
            .Include(x => x.DriverKycs)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return users.Select(AdminDriverResponse.From).ToList();
    }

    private static IReadOnlyList<AdminDriverResponse> FilterDrivers(
        IReadOnlyList<AdminDriverResponse> drivers,
        string status)
    {
        return status switch
        {
            "active" => drivers.Where(x => x.Status == "active").ToList(),
            "busy" => drivers.Where(x => x.WorkStatus == DriverWorkStatus.Busy.ToString()).ToList(),
            "pending_kyc" => drivers.Where(x => x.Status == "pending_kyc").ToList(),
            "blocked" => drivers.Where(x => x.Status == "blocked").ToList(),
            _ => drivers
        };
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
