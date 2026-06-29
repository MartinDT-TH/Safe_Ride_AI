using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
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
}
