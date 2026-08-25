using SafeRide.Application.Features.Trips.DTOs;
using SafeRide.Application.Features.RiskProtection;
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
        CancellationToken cancellationToken,
        TripEndReason reason = TripEndReason.NORMAL_COMPLETION);

    Task<TripEndReconciliationResult> RequestEndTripReconciliationAsync(
        Guid driverId,
        long tripId,
        TripEndReason reason,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    Task<TripEndReconciliationResult> ResolveEndTripReconciliationAsync(
        Guid staffUserId,
        long tripId,
        long requestId,
        bool approved,
        string? resolutionNote,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    Task ConfirmReturnByCustomerAsync(
        Guid customerId,
        long tripId,
        bool vehicleReturnedConfirmed,
        CancellationToken cancellationToken,
        int? ratingScore = null,
        string? comment = null);

    /// <summary>
    /// Driver confirms return on behalf of the customer.
    /// Requires 1–3 evidence photos. Server reads GPS from Redis; the driver
    /// cannot inject timestamp or coordinates directly.
    /// Requires successful payment, then completes the trip after return confirmation.
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

    Task AdvanceAfterSuccessfulPaymentAsync(
        Guid userId,
        long tripId,
        CancellationToken cancellationToken);

    Task SafetyTerminateAsync(
        Guid userId,
        bool isStaff,
        long tripId,
        string reason,
        CancellationToken cancellationToken);
    Task EnsureCanSafetyTerminateAsync(
        Guid userId,
        bool isStaff,
        long tripId,
        string reason,
        CancellationToken cancellationToken) => Task.CompletedTask;

    Task SafetyTerminateAsync(
        Guid userId,
        bool isStaff,
        long tripId,
        string reason,
        IReadOnlyList<StoredSafetyTerminationEvidence> evidence,
        CancellationToken cancellationToken);
}
