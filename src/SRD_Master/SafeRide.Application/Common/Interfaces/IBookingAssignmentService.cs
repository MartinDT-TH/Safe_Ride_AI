using SafeRide.Application.Features.Bookings.Commands.CreateBooking;

namespace SafeRide.Application.Common.Interfaces;

public interface IBookingAssignmentService
{
    Task<CreateBookingResponse> ConfirmDriverAsync(
        Guid customerId,
        long bookingId,
        long? offerId,
        CancellationToken cancellationToken);

    Task<CreateBookingResponse> RejectDriverAsync(
        Guid customerId,
        long bookingId,
        CancellationToken cancellationToken);

    Task<CreateBookingResponse> AcceptDriverOfferAsync(
        Guid driverId,
        long offerId,
        CancellationToken cancellationToken);

    Task RejectDriverOfferAsync(
        Guid driverId,
        long offerId,
        CancellationToken cancellationToken);
}
