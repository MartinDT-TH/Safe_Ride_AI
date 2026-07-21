using SafeRide.Domain.Entities;

namespace SafeRide.Application.Common.Interfaces;

public interface IReportRepository
{
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
