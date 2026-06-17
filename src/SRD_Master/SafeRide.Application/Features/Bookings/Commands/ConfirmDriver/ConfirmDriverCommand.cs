using MediatR;
using SafeRide.Application.Features.Bookings.Commands.CreateBooking;

namespace SafeRide.Application.Features.Bookings.Commands.ConfirmDriver;

public sealed record ConfirmDriverCommand(
    Guid CustomerId,
    long BookingId) : IRequest<CreateBookingResponse>;
