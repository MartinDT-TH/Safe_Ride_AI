using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Promotions.DTOs;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Repositories;

public sealed class PromotionRepository : IPromotionRepository, IAdminPromotionRepository
{
    private readonly ApplicationDbContext _dbContext;

    public PromotionRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminPromotionsPageData> GetAdminPromotionsAsync(
        int page,
        int pageSize,
        string? search,
        string status,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Promotions.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(promotion =>
                promotion.PromotionCode.Contains(search));
        }

        var total = await query.CountAsync(cancellationToken);
        var active = await query.CountAsync(
            promotion => promotion.IsActive
                && promotion.StartDate <= utcNow
                && promotion.EndDate >= utcNow,
            cancellationToken);
        var inactive = await query.CountAsync(
            promotion => !promotion.IsActive,
            cancellationToken);
        var expired = await query.CountAsync(
            promotion => promotion.IsActive && promotion.EndDate < utcNow,
            cancellationToken);

        query = status switch
        {
            "active" => query.Where(promotion => promotion.IsActive
                && promotion.StartDate <= utcNow
                && promotion.EndDate >= utcNow),
            "inactive" => query.Where(promotion => !promotion.IsActive),
            "expired" => query.Where(promotion =>
                promotion.IsActive && promotion.EndDate < utcNow),
            "upcoming" => query.Where(promotion =>
                promotion.IsActive && promotion.StartDate > utcNow),
            _ => query
        };

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(promotion => promotion.StartDate)
            .ThenByDescending(promotion => promotion.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new AdminPromotionsPageData(
            items,
            total,
            active,
            inactive,
            expired,
            totalItems);
    }

    public Task<Promotion?> GetByIdAsync(
        long promotionId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Promotions
            .FirstOrDefaultAsync(
                promotion => promotion.Id == promotionId,
                cancellationToken);
    }

    public Task<bool> CodeExistsAsync(
        string promotionCode,
        long? excludedPromotionId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Promotions.AnyAsync(
            promotion => promotion.PromotionCode == promotionCode
                && (!excludedPromotionId.HasValue
                    || promotion.Id != excludedPromotionId.Value),
            cancellationToken);
    }

    public async Task AddAsync(
        Promotion promotion,
        CancellationToken cancellationToken)
    {
        await _dbContext.Promotions.AddAsync(promotion, cancellationToken);
    }

    public async Task<IReadOnlyList<Promotion>> GetAvailablePromotionsAsync(
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Promotions
            .AsNoTracking()
            .Where(promotion =>
                promotion.IsActive &&
                promotion.StartDate <= utcNow &&
                promotion.EndDate >= utcNow &&
                promotion.CurrentUsageCount < promotion.MaxUsageCount)
            .OrderBy(promotion => promotion.EndDate)
            .ToListAsync(cancellationToken);
    }

    public Task<Promotion?> GetPromotionByCodeAsync(
        string promotionCode,
        CancellationToken cancellationToken)
    {
        return _dbContext.Promotions
            .FirstOrDefaultAsync(
                promotion => promotion.PromotionCode == promotionCode,
                cancellationToken);
    }

    public Task<Booking?> GetBookingForPromotionAsync(
        long bookingId,
        CancellationToken cancellationToken)
    {
        return _dbContext.Bookings
            .Include(booking => booking.BookingPromotions)
            .FirstOrDefaultAsync(
                booking => booking.BookingId == bookingId,
                cancellationToken);
    }

    public Task<int> CountCustomerPromotionUsageAsync(
        Guid customerId,
        long promotionId,
        CancellationToken cancellationToken)
    {
        return _dbContext.BookingPromotions
            .CountAsync(
                bookingPromotion =>
                    bookingPromotion.PromotionId == promotionId &&
                    bookingPromotion.Booking.CustomerId == customerId &&
                    bookingPromotion.Booking.BookingStatus == BookingStatus.Completed,
                cancellationToken);
    }

    public async Task AddBookingPromotionAsync(
        BookingPromotion bookingPromotion,
        CancellationToken cancellationToken)
    {
        await _dbContext.BookingPromotions.AddAsync(
            bookingPromotion,
            cancellationToken);
    }

    public async Task RemoveBookingPromotionsForBookingAsync(
        long bookingId,
        CancellationToken cancellationToken)
    {
        var bookingPromotions = await _dbContext.BookingPromotions
            .Where(bookingPromotion => bookingPromotion.BookingId == bookingId)
            .ToListAsync(cancellationToken);

        _dbContext.BookingPromotions.RemoveRange(bookingPromotions);
    }
}
