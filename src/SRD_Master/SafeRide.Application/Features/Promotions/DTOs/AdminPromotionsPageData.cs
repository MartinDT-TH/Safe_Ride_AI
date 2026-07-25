using SafeRide.Domain.Entities;

namespace SafeRide.Application.Features.Promotions.DTOs;

public sealed record AdminPromotionsPageData(
    IReadOnlyList<Promotion> Items,
    int Total,
    int Active,
    int Inactive,
    int Expired,
    int TotalItems);
