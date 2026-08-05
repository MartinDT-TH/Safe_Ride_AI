using SafeRide.Application.Features.Promotions.DTOs;
using SafeRide.Domain.Entities;

namespace SafeRide.Application.Common.Interfaces;

public interface IAdminPromotionRepository
{
    Task<AdminPromotionsPageData> GetAdminPromotionsAsync(
        int page,
        int pageSize,
        string? search,
        string status,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task<Promotion?> GetByIdAsync(
        long promotionId,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        string promotionCode,
        long? excludedPromotionId,
        CancellationToken cancellationToken);

    Task AddAsync(
        Promotion promotion,
        CancellationToken cancellationToken);
}
