using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.AdminDrivers;

public sealed record AdminDriverResponse(
    Guid Id,
    string DriverCode,
    string Name,
    string? Email,
    string? Phone,
    string? AvatarUrl,
    double? Rating,
    int Trips,
    DateTime CreatedAt,
    string? Address,
    string Status,
    string WorkStatus,
    bool IsActive,
    string? BanReason,
    string? CitizenId,
    DateOnly? DateOfBirth,
    string? Gender,
    IReadOnlyCollection<AdminDriverDocumentResponse> Documents)
{
    public static AdminDriverResponse From(AspNetUser user)
    {
        var documents = user.DriverKycs
            .Select(AdminDriverDocumentResponse.From)
            .ToArray();
        var pendingKyc = documents.Any(x => x.KycStatus == KycStatus.Pending.ToString());
        var status = !user.IsActive
            ? "blocked"
            : pendingKyc
                ? "pending_kyc"
                : "active";

        return new AdminDriverResponse(
            user.Id,
            $"SR-{user.CreatedAt:yyyyMMdd}-{user.Id.ToString()[..6].ToUpperInvariant()}",
            user.FullName ?? user.UserName ?? "Tài xế",
            user.Email,
            user.PhoneNumber,
            user.AvatarUrl,
            user.DriverProfile?.Ratings.Count > 0
                ? user.DriverProfile.Ratings.Average(x => x.RatingScore)
                : null,
            user.DriverProfile?.Ratings.Count ?? 0,
            user.CreatedAt,
            user.DriverProfile?.HomeAddress,
            status,
            user.DriverProfile?.WorkStatus.ToString() ?? DriverWorkStatus.Offline.ToString(),
            user.IsActive,
            user.BanReason,
            user.DriverProfile?.IdentityCardNumber,
            user.DateOfBirth,
            user.Gender,
            documents);
    }
}
