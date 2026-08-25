using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Drivers.DTOs;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class DriverMatchingPreferencesService : IDriverMatchingPreferencesService
{
    private readonly ApplicationDbContext _dbContext;

    public DriverMatchingPreferencesService(ApplicationDbContext dbContext) =>
        _dbContext = dbContext;

    public async Task<DriverMatchingPreferencesDto> GetAsync(
        Guid driverId,
        CancellationToken cancellationToken)
    {
        var preferences = await _dbContext.DriverProfiles
            .AsNoTracking()
            .Where(profile => profile.DriverId == driverId)
            .Select(profile => new DriverMatchingPreferencesDto(
                profile.AcceptLongPickupTrips,
                profile.AcceptLongDistanceTrips))
            .SingleOrDefaultAsync(cancellationToken);

        return preferences ?? throw new KeyNotFoundException("Driver profile was not found.");
    }

    public async Task<DriverMatchingPreferencesDto> UpdateAsync(
        Guid driverId,
        bool acceptLongPickupTrips,
        bool acceptLongDistanceTrips,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.DriverProfiles
            .SingleOrDefaultAsync(x => x.DriverId == driverId, cancellationToken)
            ?? throw new KeyNotFoundException("Driver profile was not found.");

        profile.AcceptLongPickupTrips = acceptLongPickupTrips;
        profile.AcceptLongDistanceTrips = acceptLongDistanceTrips;
        profile.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new DriverMatchingPreferencesDto(
            profile.AcceptLongPickupTrips,
            profile.AcceptLongDistanceTrips);
    }
}
