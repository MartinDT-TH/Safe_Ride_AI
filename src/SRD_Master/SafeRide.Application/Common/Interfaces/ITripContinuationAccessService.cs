using System.Security.Claims;
using SafeRide.Application.Features.Auth;

namespace SafeRide.Application.Common.Interfaces;

public interface ITripContinuationAccessService
{
    Task<bool> IsAllowedAsync(
        ClaimsPrincipal user,
        TripContinuationOperation operation,
        long? tripId = null,
        long? bookingId = null,
        CancellationToken cancellationToken = default);
}
