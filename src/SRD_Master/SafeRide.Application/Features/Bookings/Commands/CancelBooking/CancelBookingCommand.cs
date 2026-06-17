using MediatR;

namespace SafeRide.Application.Features.Bookings.Commands.CancelBooking;

public sealed record CancelBookingCommand(
    Guid CustomerId,
    long BookingId,
    string? Reason) : IRequest<CancelBookingResponse>;