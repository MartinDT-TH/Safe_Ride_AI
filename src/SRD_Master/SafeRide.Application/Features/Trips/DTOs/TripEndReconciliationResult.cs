using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Trips.DTOs;

public sealed record TripEndReconciliationResult(
    long RequestId,
    long TripId,
    TripEndReason RequestedReason,
    TripEndReconciliationStatus Status,
    DateTime RequestedAtUtc,
    DateTime? ResolvedAtUtc,
    string Message);
