using MediatR;

namespace SafeRide.Application.Features.AdminTrips.Queries.GetAdminTripDetails;

public sealed record GetAdminTripDetailsQuery(long TripId)
    : IRequest<AdminTripDetailsResponse?>;
