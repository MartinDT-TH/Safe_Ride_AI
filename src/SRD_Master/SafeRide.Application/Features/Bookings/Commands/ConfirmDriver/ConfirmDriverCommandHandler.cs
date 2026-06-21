using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings.Commands.CreateBooking;

namespace SafeRide.Application.Features.Bookings.Commands.ConfirmDriver;

public sealed class ConfirmDriverCommandHandler
    : IRequestHandler<ConfirmDriverCommand, CreateBookingResponse>
{
    private readonly IBookingAssignmentService _assignmentService;

    public ConfirmDriverCommandHandler(IBookingAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    public Task<CreateBookingResponse> Handle(
        ConfirmDriverCommand request,
        CancellationToken cancellationToken)
    {
        return _assignmentService.ConfirmDriverAsync(
            request.CustomerId,
            request.BookingId,
            request.OfferId,
            cancellationToken);
    }
}
