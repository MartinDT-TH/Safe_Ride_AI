using SafeRide.Domain.Entities;
using SafeRide.Application.Features.AdminReports;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Interfaces;

public interface IReportRepository
{
    Task<AdminReportPagedResult> GetAdminReportsAsync(
        AdminReportListFilter filter,
        CancellationToken cancellationToken = default);

    Task<AdminReportResponse?> GetAdminReportByIdAsync(
        long reportId,
        CancellationToken cancellationToken = default);

    Task<Report?> GetReportForUpdateAsync(
        long reportId,
        CancellationToken cancellationToken = default);

    Task<Booking?> GetBookingForReportAsync(
        long bookingId,
        CancellationToken cancellationToken = default);

    Task<Trip?> GetTripForReportAsync(
        long tripId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByTripAndUserAsync(
        long tripId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AddReportAsync(
        Report report,
        CancellationToken cancellationToken = default);
}
