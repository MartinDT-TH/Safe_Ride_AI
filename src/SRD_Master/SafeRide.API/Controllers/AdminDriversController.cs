using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/drivers")]
public sealed class AdminDriversController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<AspNetUser> _userManager;

    public AdminDriversController(ApplicationDbContext db, UserManager<AspNetUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetDrivers([FromQuery] string status = "all", CancellationToken cancellationToken = default)
    {
        var driverUsers = await _userManager.GetUsersInRoleAsync("Driver");
        var pendingApplicantIds = await _db.DriverKycs.AsNoTracking()
            .Where(x => x.KycStatus == KycStatus.Pending)
            .Select(x => x.DriverId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var driverIds = driverUsers.Select(x => x.Id)
            .Concat(pendingApplicantIds)
            .Distinct()
            .ToArray();
        var drivers = await LoadDrivers(driverIds, cancellationToken);
        var counts = new
        {
            all = drivers.Count,
            active = drivers.Count(x => x.Status == "active"),
            busy = drivers.Count(x => x.WorkStatus == DriverWorkStatus.Busy.ToString()),
            pendingKyc = drivers.Count(x => x.Status == "pending_kyc"),
            blocked = drivers.Count(x => x.Status == "blocked")
        };

        var filtered = status switch
        {
            "active" => drivers.Where(x => x.Status == "active"),
            "busy" => drivers.Where(x => x.WorkStatus == DriverWorkStatus.Busy.ToString()),
            "pending_kyc" => drivers.Where(x => x.Status == "pending_kyc"),
            "blocked" => drivers.Where(x => x.Status == "blocked"),
            _ => drivers
        };

        return Ok(new { drivers = filtered, counts });
    }

    [HttpPatch("{driverId:guid}/block")]
    public async Task<IActionResult> Block(Guid driverId, [FromBody] BlockDriverRequest request, CancellationToken cancellationToken)
    {
        var user = await FindDriver(driverId);
        if (user is null) return NotFound(new { message = "Không tìm thấy tài xế." });
        user.IsActive = false;
        user.BanReason = string.IsNullOrWhiteSpace(request.Reason) ? "Khóa bởi quản trị viên" : request.Reason.Trim();
        user.UpdatedAt = DateTime.UtcNow;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(new { message = string.Join("; ", result.Errors.Select(x => x.Description)) });
        return Ok((await LoadDrivers([driverId], cancellationToken)).Single());
    }

    [HttpPatch("{driverId:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid driverId, CancellationToken cancellationToken)
    {
        var user = await FindDriver(driverId);
        if (user is null) return NotFound(new { message = "Không tìm thấy tài xế." });
        user.IsActive = true;
        user.BanReason = null;
        user.UpdatedAt = DateTime.UtcNow;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(new { message = string.Join("; ", result.Errors.Select(x => x.Description)) });
        return Ok((await LoadDrivers([driverId], cancellationToken)).Single());
    }

    [HttpPatch("{driverId:guid}/kyc")]
    public async Task<IActionResult> ReviewKyc(Guid driverId, [FromBody] ReviewKycRequest request, CancellationToken cancellationToken)
    {
        if (request.Status is not KycStatus.Approved and not KycStatus.Rejected)
            return BadRequest(new { message = "Trạng thái KYC phải là Approved hoặc Rejected." });
        if (request.Status == KycStatus.Rejected && string.IsNullOrWhiteSpace(request.RejectionReason))
            return BadRequest(new { message = "Cần nhập lý do từ chối hồ sơ." });
        var applicant = await _userManager.FindByIdAsync(driverId.ToString());
        if (applicant is null) return NotFound(new { message = "Không tìm thấy người nộp hồ sơ." });

        var documents = await _db.DriverKycs.Where(x => x.DriverId == driverId).ToListAsync(cancellationToken);
        if (documents.Count == 0) return NotFound(new { message = "Tài xế chưa có hồ sơ KYC." });
        var now = DateTime.UtcNow;
        foreach (var document in documents.Where(x => x.KycStatus == KycStatus.Pending))
        {
            document.KycStatus = request.Status;
            document.VerifiedAt = now;
            document.RejectionReason = request.Status == KycStatus.Rejected ? request.RejectionReason?.Trim() : null;
        }

        if (request.Status == KycStatus.Approved)
        {
            var identityCardNumber = documents
                .FirstOrDefault(x => x.DocumentType == KycDocumentType.ID_CARD)
                ?.DocumentNumber;
            if (string.IsNullOrWhiteSpace(identityCardNumber))
                return BadRequest(new { message = "Hồ sơ chưa có số CCCD nên chưa thể phê duyệt tài xế." });

            var profile = await _db.DriverProfiles.FindAsync([driverId], cancellationToken);
            if (profile is null)
            {
                _db.DriverProfiles.Add(new DriverProfile
                {
                    DriverId = driverId,
                    IdentityCardNumber = identityCardNumber,
                    WorkStatus = DriverWorkStatus.Offline,
                    CreatedAt = now
                });
            }
            else
            {
                profile.IdentityCardNumber = identityCardNumber;
                profile.UpdatedAt = now;
            }
        }
        await _db.SaveChangesAsync(cancellationToken);

        if (request.Status == KycStatus.Approved && !await _userManager.IsInRoleAsync(applicant, "Driver"))
        {
            var roleResult = await _userManager.AddToRoleAsync(applicant, "Driver");
            if (!roleResult.Succeeded)
                return BadRequest(new { message = string.Join("; ", roleResult.Errors.Select(x => x.Description)) });
        }
        return Ok((await LoadDrivers([driverId], cancellationToken)).Single());
    }

    private async Task<AspNetUser?> FindDriver(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        return user is not null && await _userManager.IsInRoleAsync(user, "Driver") ? user : null;
    }

    private async Task<List<AdminDriverResponse>> LoadDrivers(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var idArray = ids.ToArray();
        var users = await _db.Users.AsNoTracking()
            .Where(x => idArray.Contains(x.Id))
            .Include(x => x.DriverProfile).ThenInclude(x => x!.Ratings)
            .Include(x => x.DriverKycs)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return users.Select(AdminDriverResponse.From).ToList();
    }
}

public sealed record BlockDriverRequest(string? Reason);
public sealed record ReviewKycRequest(KycStatus Status, string? RejectionReason);

public sealed record AdminDriverResponse(
    Guid Id, string DriverCode, string Name, string? Email, string? Phone,
    string? AvatarUrl, double? Rating, int Trips, DateTime CreatedAt, string? Address,
    string Status, string WorkStatus, bool IsActive, string? BanReason,
    string? CitizenId, DateOnly? DateOfBirth, string? Gender, IReadOnlyCollection<AdminDriverDocumentResponse> Documents)
{
    public static AdminDriverResponse From(AspNetUser user)
    {
        var documents = user.DriverKycs.Select(AdminDriverDocumentResponse.From).ToArray();
        var pending = documents.Any(x => x.KycStatus == KycStatus.Pending.ToString());
        var status = !user.IsActive ? "blocked" : pending ? "pending_kyc" : "active";
        return new(user.Id, $"SR-{user.CreatedAt:yyyyMMdd}-{user.Id.ToString()[..6].ToUpperInvariant()}",
            user.FullName ?? user.UserName ?? "Tài xế", user.Email, user.PhoneNumber, user.AvatarUrl,
            user.DriverProfile?.Ratings.Count > 0 ? user.DriverProfile.Ratings.Average(x => x.RatingScore) : null,
            user.DriverProfile?.Ratings.Count ?? 0, user.CreatedAt, user.DriverProfile?.HomeAddress,
            status, user.DriverProfile?.WorkStatus.ToString() ?? DriverWorkStatus.Offline.ToString(),
            user.IsActive, user.BanReason, user.DriverProfile?.IdentityCardNumber,
            user.DateOfBirth, user.Gender, documents);
    }
}

public sealed record AdminDriverDocumentResponse(
    long Id, string DocumentType, string? DocumentNumber, string? LicenseClass,
    string? FrontImageUrl, string? BackImageUrl, string? FileUrl,
    DateOnly? IssueDate, DateOnly? ExpiryDate, string KycStatus,
    DateTime CreatedAt, DateTime? VerifiedAt, string? RejectionReason)
{
    public static AdminDriverDocumentResponse From(DriverKyc x) => new(
        x.Id, x.DocumentType.ToString(), x.DocumentNumber, x.LicenseClass?.ToString(),
        x.FrontImageUrl, x.BackImageUrl, x.FileUrl, x.IssueDate, x.ExpiryDate,
        x.KycStatus.ToString(), x.CreatedAt, x.VerifiedAt, x.RejectionReason);
}
