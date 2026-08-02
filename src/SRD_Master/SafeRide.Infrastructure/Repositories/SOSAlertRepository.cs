using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.AdminSOSAlerts;
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
                .ThenInclude(booking => booking.Customer)
            .Include(trip => trip.Driver)
                .ThenInclude(driver => driver.Driver)
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

    public async Task<AdminSOSAlertPagedResult> GetAdminAlertsAsync(
        SOSStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var query = ProjectAdminAlerts(status);
        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 1
            : (int)Math.Ceiling(totalItems / (double)normalizedPageSize);
        var currentPage = Math.Min(normalizedPage, totalPages);
        var items = await query
            .OrderByDescending(alert => alert.CreatedAt)
            .ThenByDescending(alert => alert.SosAlertId)
            .Skip((currentPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new AdminSOSAlertPagedResult(
            items,
            currentPage,
            normalizedPageSize,
            totalItems,
            totalPages);
    }

    public Task<AdminSOSAlertResponse?> GetAdminAlertByIdAsync(
        long sosAlertId,
        CancellationToken cancellationToken = default)
    {
        return ProjectAdminAlerts(status: null)
            .FirstOrDefaultAsync(
                alert => alert.SosAlertId == sosAlertId,
                cancellationToken);
    }

    private IQueryable<AdminSOSAlertResponse> ProjectAdminAlerts(SOSStatus? status)
    {
        var alerts = _dbContext.Sosalerts.AsNoTracking();
        if (status.HasValue)
        {
            alerts = alerts.Where(alert => alert.SOSStatus == status.Value);
        }

        return
            from alert in alerts
            join trip in _dbContext.Trips.AsNoTracking()
                on alert.TripId equals trip.Id
            join customer in _dbContext.Users.AsNoTracking()
                on alert.TriggeredByUserId equals customer.Id into customers
            from customer in customers.DefaultIfEmpty()
            join driver in _dbContext.Users.AsNoTracking()
                on trip.DriverId equals driver.Id into drivers
            from driver in drivers.DefaultIfEmpty()
            select new AdminSOSAlertResponse(
                alert.Id,
                alert.TripId,
                trip.BookingId,
                alert.TriggeredByUserId,
                customer == null ? null : customer.FullName,
                customer == null ? null : customer.PhoneNumber,
                driver == null ? null : driver.Id,
                driver == null ? null : driver.FullName,
                driver == null ? null : driver.PhoneNumber,
                alert.Location.Y,
                alert.Location.X,
                alert.EmergencyMessage,
                alert.SOSStatus,
                alert.CreatedAt);
    }
}
