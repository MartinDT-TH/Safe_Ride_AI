using SafeRide.Application.Features.Bookings.DTOs;

namespace SafeRide.Application.Common.Interfaces;

public interface IBookingMatchingService
{
    Task<BookingDriverOfferDto?> StartMatchingAsync(long bookingId, CancellationToken cancellationToken);
}
