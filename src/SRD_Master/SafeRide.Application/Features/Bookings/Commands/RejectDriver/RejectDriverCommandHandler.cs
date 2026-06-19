using MediatR;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Bookings.Commands.CreateBooking;

namespace SafeRide.Application.Features.Bookings.Commands.RejectDriver;

public sealed class RejectDriverCommandHandler
    : IRequestHandler<RejectDriverCommand, CreateBookingResponse>
{
    private readonly IBookingAssignmentService _assignmentService;

    public RejectDriverCommandHandler(
        IBookingAssignmentService assignmentService)
    {
        _assignmentService = assignmentService;
    }

    public Task<CreateBookingResponse> Handle(
        RejectDriverCommand request,
        CancellationToken cancellationToken)
    {
        return _assignmentService.RejectDriverAsync(
            request.CustomerId,
            request.BookingId,
            cancellationToken);
    }
}
