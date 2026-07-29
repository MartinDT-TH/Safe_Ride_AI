using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Domain.Entities;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Repositories;

public sealed class ReportRepository : IReportRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ReportRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Booking?> GetBookingForReportAsync(
        long bookingId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Bookings
            .AsNoTracking()
            .Where(booking => booking.BookingId == bookingId)
            .Select(booking => new Booking
            {
                BookingId = booking.BookingId,
                CustomerId = booking.CustomerId,
                Trip = booking.Trip == null
                    ? null
                    : new Trip
                    {
                        Id = booking.Trip.Id,
                        TripStatus = booking.Trip.TripStatus
                    }
            })
            .FirstOrDefaultAsync(
                cancellationToken);
    }

    public Task<Trip?> GetTripForReportAsync(
        long tripId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Trips
            .Include(trip => trip.Booking)
            .FirstOrDefaultAsync(
                trip => trip.Id == tripId,
                cancellationToken);
    }

    public Task<bool> ExistsByTripAndUserAsync(
        long tripId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Reports
            .AsNoTracking()
            .AnyAsync(
                report => report.TripId == tripId && report.UserId == userId,
                cancellationToken);
    }

    public async Task AddReportAsync(
        Report report,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Reports.AddAsync(report, cancellationToken);
    }
}
