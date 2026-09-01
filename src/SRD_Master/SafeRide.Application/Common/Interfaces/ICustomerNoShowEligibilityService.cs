using SafeRide.Application.Common.Models;

namespace SafeRide.Application.Common.Interfaces;

public interface ICustomerNoShowEligibilityService
{
    Task<CustomerNoShowEligibilityResponse> GetAsync(
        Guid driverId,
        long tripId,
        CancellationToken cancellationToken);
}
