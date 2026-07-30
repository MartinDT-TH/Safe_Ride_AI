using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Repositories;

public sealed class SOSAlertRepository : ISOSAlertRepository
{
    private readonly ApplicationDbContext _dbContext;

    public SOSAlertRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Trip?> GetTripForSOSAsync(
        long tripId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Trips
            .Include(trip => trip.Booking)
            .FirstOrDefaultAsync(
                trip => trip.Id == tripId,
                cancellationToken);
    }

    public Task<SOSAlert?> GetActiveAlertByTripIdAsync(
        long tripId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Sosalerts
            .FirstOrDefaultAsync(
                alert => alert.TripId == tripId
                    && alert.SOSStatus == SOSStatus.Active,
                cancellationToken);
    }

    public async Task AddAsync(
        SOSAlert sosAlert,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Sosalerts.AddAsync(sosAlert, cancellationToken);
    }
}
