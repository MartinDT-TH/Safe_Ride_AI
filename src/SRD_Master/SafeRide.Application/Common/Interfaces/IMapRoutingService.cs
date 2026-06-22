using SafeRide.Application.Common.Models;

namespace SafeRide.Application.Common.Interfaces;

public interface IMapRoutingService
{
    Task<RouteEstimateResult> GetRouteEstimateAsync(
        RouteEstimateRequest request,
        CancellationToken cancellationToken);
}
