using SafeRide.Application.Features.Trips.DTOs;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Interfaces;

public interface ITripStatusService
{
    Task UpdateDriverTripStatusAsync(
        Guid driverId,
        long tripId,
        TripStatus tripStatus,
        CancellationToken cancellationToken);

    Task EndTripAsync(
        Guid driverId,
        long tripId,
        CancellationToken cancellationToken);

    Task ConfirmReturnByCustomerAsync(
        Guid customerId,
        long tripId,
        bool vehicleReturnedConfirmed,
        CancellationToken cancellationToken);

    /// <summary>
    /// Driver confirms return on behalf of the customer.
    /// Requires 1–3 evidence photos. Server reads GPS from Redis; the driver
    /// cannot inject timestamp or coordinates directly.
    /// Moves trip from WAITING_RETURN_CONFIRM → RETURN_CONFIRMED.
    /// </summary>
    Task ConfirmReturnByDriverAsync(
        Guid driverId,
        long tripId,
        IReadOnlyList<ReturnEvidenceItem> evidence,
        string? note,
        CancellationToken cancellationToken);

    Task CompleteTripAsync(
        Guid userId,
        long tripId,
        CancellationToken cancellationToken);
}
