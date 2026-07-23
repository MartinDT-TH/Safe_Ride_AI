using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Contracts.Responses.Feedbacks;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Repositories;

public sealed class RatingRepository : IRatingRepository
{
    private readonly ApplicationDbContext _dbContext;

    public RatingRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Trip?> GetTripForRatingAsync(
        long tripId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Trips
            .Include(trip => trip.Booking)
            .Include(trip => trip.Rating)
            .FirstOrDefaultAsync(
                trip => trip.Id == tripId,
                cancellationToken);
    }

    public async Task AddRatingAsync(
        Rating rating,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Ratings.AddAsync(rating, cancellationToken);
    }

    public Task<bool> DriverExistsAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.DriverProfiles
            .AsNoTracking()
            .AnyAsync(dp => dp.DriverId == driverId, cancellationToken);
    }

    public Task<IReadOnlyList<DriverRatingItemResponse>> GetDriverRatingItemsAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Ratings
            .AsNoTracking()
            .Where(r => r.DriverId == driverId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new DriverRatingItemResponse(
                r.Id,
                r.TripId,
                r.Customer.FullName,
                r.Customer.AvatarUrl,
                r.RatingScore,
                r.Comment,
                r.CreatedAt))
            .ToListAsync(cancellationToken)
            .ContinueWith(
                t => (IReadOnlyList<DriverRatingItemResponse>)t.Result,
                TaskContinuationOptions.ExecuteSynchronously);
    }
}
