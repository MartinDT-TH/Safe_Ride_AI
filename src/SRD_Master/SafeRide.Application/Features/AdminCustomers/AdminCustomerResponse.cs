using SafeRide.Domain.Entities;

namespace SafeRide.Application.Features.AdminCustomers;

public sealed record AdminCustomerResponse(
    Guid Id,
    string CustomerCode,
    string Name,
    string? Email,
    string? Phone,
    string? AvatarUrl,
    DateTime CreatedAt,
    string Status,
    bool IsActive,
    string? BanReason,
    string Tier)
{
    public static AdminCustomerResponse From(AspNetUser user) => new(
        user.Id,
        $"CUS-{user.CreatedAt:yyyyMMdd}-{user.Id.ToString()[..6].ToUpperInvariant()}",
        user.FullName ?? user.UserName ?? "Khách hàng",
        user.Email,
        user.PhoneNumber,
        user.AvatarUrl,
        user.CreatedAt,
        user.IsActive ? "active" : "blocked",
        user.IsActive,
        user.BanReason,
        "standard");
}
