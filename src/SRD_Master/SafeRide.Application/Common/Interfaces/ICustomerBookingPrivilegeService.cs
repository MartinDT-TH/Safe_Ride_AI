using SafeRide.Domain.Entities;
using SafeRide.Domain.Enums;

namespace SafeRide.Application.Common.Interfaces;

public interface ICustomerBookingPrivilegeService
{
    Task<CustomerBookingPrivilege> RecalculateAsync(Guid customerId, CancellationToken cancellationToken);

    Task EnsureCanCreateAsync(
        Guid customerId,
        BookingType bookingType,
        DateTime utcNow,
        CancellationToken cancellationToken);
}
