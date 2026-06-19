using MediatR;
using SafeRide.Application.Features.Bookings.Commands.CreateBooking;

namespace SafeRide.Application.Features.Bookings.Commands.RejectDriver;

public sealed record RejectDriverCommand(
    Guid CustomerId,
    long BookingId)
    : IRequest<CreateBookingResponse>;
