using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Services;

public sealed class TripSessionQueryService : ITripSessionQueryService
{
    private readonly ApplicationDbContext _dbContext;

    public TripSessionQueryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TripSessionInfo?> GetActiveTripForUserAsync(
        Guid userId,
        DateTime? existedAtOrBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Trips
            .AsNoTracking()
            .Where(trip =>
                trip.TripStatus != TripStatus.COMPLETED
                && trip.TripStatus != TripStatus.CANCELLED
                && (trip.DriverId == userId || trip.Booking.CustomerId == userId));

        if (existedAtOrBeforeUtc.HasValue)
        {
            var cutoff = existedAtOrBeforeUtc.Value;
            query = query.Where(trip =>
                (trip.DriverAssignedAt ?? trip.CreatedAt) <= cutoff);
        }

        return await query
            .OrderByDescending(trip => trip.DriverAssignedAt ?? trip.CreatedAt)
            .Select(trip => new TripSessionInfo(
                trip.Id,
                trip.BookingId,
                trip.Booking.CustomerId,
                trip.DriverId,
                trip.TripStatus,
                trip.CreatedAt,
                trip.DriverAssignedAt,
                trip.StartedAt,
                trip.CompletedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<TripSessionInfo?> GetTripForUserAsync(
        Guid userId,
        long tripId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Trips
            .AsNoTracking()
            .Where(trip =>
                trip.Id == tripId
                && (trip.DriverId == userId || trip.Booking.CustomerId == userId))
            .Select(trip => new TripSessionInfo(
                trip.Id,
                trip.BookingId,
                trip.Booking.CustomerId,
                trip.DriverId,
                trip.TripStatus,
                trip.CreatedAt,
                trip.DriverAssignedAt,
                trip.StartedAt,
                trip.CompletedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<TripSessionInfo?> GetTripForBookingForUserAsync(
        Guid userId,
        long bookingId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Trips
            .AsNoTracking()
            .Where(trip =>
                trip.BookingId == bookingId
                && (trip.DriverId == userId || trip.Booking.CustomerId == userId))
            .Select(trip => new TripSessionInfo(
                trip.Id,
                trip.BookingId,
                trip.Booking.CustomerId,
                trip.DriverId,
                trip.TripStatus,
                trip.CreatedAt,
                trip.DriverAssignedAt,
                trip.StartedAt,
                trip.CompletedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> HasActiveTripForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Trips.AnyAsync(
            trip =>
                trip.TripStatus != TripStatus.COMPLETED
                && trip.TripStatus != TripStatus.CANCELLED
                && (trip.DriverId == userId || trip.Booking.CustomerId == userId),
            cancellationToken);
    }

    public Task<bool> HasActiveTripForDriverAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Trips.AnyAsync(
            trip =>
                trip.DriverId == driverId
                && trip.TripStatus != TripStatus.COMPLETED
                && trip.TripStatus != TripStatus.CANCELLED,
            cancellationToken);
    }
}
