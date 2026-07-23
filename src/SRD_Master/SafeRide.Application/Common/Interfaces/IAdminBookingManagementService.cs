using SafeRide.Application.Features.AdminBookings;

namespace SafeRide.Application.Common.Interfaces;

public interface IAdminBookingManagementService
{
    Task<AdminBookingPagedResult> GetBookingsAsync(
        AdminBookingListFilter filter,
        CancellationToken cancellationToken);
}
