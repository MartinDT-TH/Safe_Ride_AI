using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Promotions;
using SafeRide.Domain.Entities;

namespace SafeRide.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _dbContext;

    public UnitOfWork(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.Entries.Any(entry => entry.Entity is BookingPromotion)
            && IsBookingPromotionUniqueViolation(exception))
        {
            throw new PromotionException(
                "promotion.booking_already_applied",
                "A promotion has already been applied to this booking.",
                409);
        }
    }

    private static bool IsBookingPromotionUniqueViolation(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current.Message.Contains("UX_BookingPromotions_BookingId", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("2601", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("2627", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
