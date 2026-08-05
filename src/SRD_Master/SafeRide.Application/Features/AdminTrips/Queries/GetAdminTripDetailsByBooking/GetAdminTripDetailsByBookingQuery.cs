using MediatR;

namespace SafeRide.Application.Features.AdminTrips.Queries.GetAdminTripDetailsByBooking;

public sealed record GetAdminTripDetailsByBookingQuery(long BookingId)
    : IRequest<AdminTripDetailsResponse?>;
