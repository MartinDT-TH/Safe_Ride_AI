using SafeRide.Application.Common.Models;

namespace SafeRide.Application.Common.Interfaces;

public interface IGoogleMapsService
{
    Task<RouteEstimateResult> GetRouteEstimateAsync(
        LocationPoint pickup,
        LocationPoint destination,
        CancellationToken cancellationToken);
}
