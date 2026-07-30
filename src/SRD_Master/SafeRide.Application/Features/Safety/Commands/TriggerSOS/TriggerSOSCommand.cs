using MediatR;

namespace SafeRide.Application.Features.Safety.Commands.TriggerSOS;

public sealed record TriggerSOSCommand(
    long TripId,
    Guid CustomerId,
    double Latitude,
    double Longitude,
    string? Message) : IRequest<TriggerSOSResponse>;
