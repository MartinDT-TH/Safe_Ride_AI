using MediatR;
using SafeRide.Application.Features.Bookings.DTOs;

namespace SafeRide.Application.Features.Bookings.Queries.GetBookingDetails;

public sealed record GetBookingDetailsQuery(
    Guid UserId,
    long BookingId) : IRequest<BookingDetailsDto>;
