using SafeRide.Application.Features.AdminTrips;

namespace SafeRide.Application.Common.Interfaces;

public interface IAdminTripManagementService
{
    Task<AdminTripDetailsResponse?> GetTripDetailsByTripIdAsync(
        long tripId,
        CancellationToken cancellationToken);

    Task<AdminTripDetailsResponse?> GetTripDetailsByBookingIdAsync(
        long bookingId,
        CancellationToken cancellationToken);
}
