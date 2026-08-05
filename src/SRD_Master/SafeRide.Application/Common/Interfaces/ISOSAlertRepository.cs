using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;
using SafeRide.Application.Features.AdminSOSAlerts;

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

    Task<AdminSOSAlertPagedResult> GetAdminAlertsAsync(
        SOSStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminSOSAlertResponse?> GetAdminAlertByIdAsync(
        long sosAlertId,
        CancellationToken cancellationToken = default);
}
