using Microsoft.EntityFrameworkCore;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.AdminReports;
using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Infrastructure.Persistence;

namespace SafeRide.Infrastructure.Repositories;

public sealed class ReportRepository : IReportRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ReportRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminReportPagedResult> GetAdminReportsAsync(
        AdminReportListFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = filter.Page <= 0 ? 1 : filter.Page;
        var pageSize = filter.PageSize <= 0 ? 10 : Math.Min(filter.PageSize, 100);
        var query = _dbContext.Reports.AsNoTracking();

        if (filter.Status.HasValue)
        {
            query = query.Where(report => report.Status == filter.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var pattern = $"%{filter.Search.Trim()}%";
            query = query.Where(report =>
                EF.Functions.Like(report.Subject, pattern)
                || EF.Functions.Like(report.Description, pattern)
                || (report.User.FullName != null
                    && EF.Functions.Like(report.User.FullName, pattern))
                || (report.User.Email != null
                    && EF.Functions.Like(report.User.Email, pattern))
                || (report.User.PhoneNumber != null
                    && EF.Functions.Like(report.User.PhoneNumber, pattern)));
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 1
            : (int)Math.Ceiling(totalItems / (double)pageSize);
        var currentPage = Math.Min(page, totalPages);
        var reports = await ProjectAdminReports(query)
            .OrderByDescending(report => report.CreatedAt)
            .ThenByDescending(report => report.Id)
            .Skip((currentPage - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new AdminReportPagedResult(
            reports.Select(MapAdminReport).ToArray(),
            currentPage,
            pageSize,
            totalItems,
            totalPages);
    }

    public async Task<AdminReportResponse?> GetAdminReportByIdAsync(
        long reportId,
        CancellationToken cancellationToken = default)
    {
        var report = await ProjectAdminReports(
                _dbContext.Reports
                    .AsNoTracking()
                    .Where(item => item.Id == reportId))
            .FirstOrDefaultAsync(item => item.Id == reportId, cancellationToken);

        return report is null ? null : MapAdminReport(report);
    }

    public Task<Report?> GetReportForUpdateAsync(
        long reportId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Reports.FirstOrDefaultAsync(
            report => report.Id == reportId,
            cancellationToken);
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

    private IQueryable<AdminReportProjection> ProjectAdminReports(IQueryable<Report> reports)
    {
        return
            from report in reports
            join trip in _dbContext.Trips.AsNoTracking()
                on report.TripId equals (long?)trip.Id into tripGroup
            from trip in tripGroup.DefaultIfEmpty()
            join driver in _dbContext.Users.AsNoTracking()
                on (Guid?)(trip == null ? null : trip.DriverId) equals (Guid?)driver.Id into driverGroup
            from driver in driverGroup.DefaultIfEmpty()
            select new AdminReportProjection
            {
                Id = report.Id,
                TripId = report.TripId,
                BookingId = trip == null ? null : trip.BookingId,
                ReporterUserId = report.UserId,
                ReporterName = report.User.FullName
                    ?? report.User.Email
                    ?? report.User.PhoneNumber
                    ?? report.UserId.ToString(),
                ReporterEmail = report.User.Email,
                ReporterPhone = report.User.PhoneNumber,
                DriverId = driver == null ? null : driver.Id,
                DriverName = driver == null ? null : driver.FullName,
                DriverEmail = driver == null ? null : driver.Email,
                DriverPhoneNumber = driver == null ? null : driver.PhoneNumber,
                Subject = report.Subject,
                Description = report.Description,
                Status = report.Status,
                CreatedAt = report.CreatedAt
            };
    }

    private static AdminReportResponse MapAdminReport(AdminReportProjection report)
    {
        var status = Enum.IsDefined(report.Status)
            ? report.Status
            : ReportStatus.Pending;

        return new AdminReportResponse(
            report.Id,
            report.TripId,
            report.BookingId,
            report.ReporterUserId,
            report.ReporterName,
            report.ReporterEmail,
            report.ReporterPhone,
            report.DriverId,
            report.DriverName,
            report.DriverEmail,
            report.DriverPhoneNumber,
            report.Subject,
            report.Description,
            status.ToString(),
            report.CreatedAt);
    }

    private sealed class AdminReportProjection
    {
        public long Id { get; init; }
        public long? TripId { get; init; }
        public long? BookingId { get; init; }
        public Guid ReporterUserId { get; init; }
        public string ReporterName { get; init; } = null!;
        public string? ReporterEmail { get; init; }
        public string? ReporterPhone { get; init; }
        public Guid? DriverId { get; init; }
        public string? DriverName { get; init; }
        public string? DriverEmail { get; init; }
        public string? DriverPhoneNumber { get; init; }
        public string Subject { get; init; } = null!;
        public string Description { get; init; } = null!;
        public ReportStatus Status { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
