using SafeRide.Application.Features.Bookings.Commands.CreateBooking;

namespace SafeRide.Application.Common.Interfaces;

public interface IBookingAssignmentService
{
    Task<CreateBookingResponse> ConfirmDriverAsync(
        Guid customerId,
        long bookingId,
        CancellationToken cancellationToken);
}
