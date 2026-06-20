using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Repositories;

public sealed class PromotionRepository : IPromotionRepository
{
    private readonly ApplicationDbContext _dbContext;

    public PromotionRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
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
