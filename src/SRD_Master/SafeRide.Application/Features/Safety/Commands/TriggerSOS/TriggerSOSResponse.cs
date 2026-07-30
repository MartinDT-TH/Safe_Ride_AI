using SafeRide.Domain.Enums;

namespace SafeRide.Application.Features.Safety.Commands.TriggerSOS;

public sealed record TriggerSOSResponse(
    long SosAlertId,
    long TripId,
    SOSStatus Status,
    string Message,
    DateTime CreatedAt);
