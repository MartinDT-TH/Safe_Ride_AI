using SafeRide.Domain.Entities;

namespace SafeRide.Application.Common.Interfaces;

public interface ISOSAlertRepository
{
    Task<Trip?> GetTripForSOSAsync(
        long tripId,
        CancellationToken cancellationToken = default);

    Task<SOSAlert?> GetActiveAlertByTripIdAsync(
        long tripId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        SOSAlert sosAlert,
        CancellationToken cancellationToken = default);
}
