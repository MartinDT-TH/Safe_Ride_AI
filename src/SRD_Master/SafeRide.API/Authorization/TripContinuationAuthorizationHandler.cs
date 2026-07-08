using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.Auth;

namespace SafeRide.API.Authorization;

public sealed class TripContinuationRequirement : IAuthorizationRequirement;

public sealed class TripContinuationAuthorizationHandler
    : AuthorizationHandler<TripContinuationRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITripContinuationAccessService _accessService;

    public TripContinuationAuthorizationHandler(
        IHttpContextAccessor httpContextAccessor,
        ITripContinuationAccessService accessService)
    {
        _httpContextAccessor = httpContextAccessor;
        _accessService = accessService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TripContinuationRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (!IsContinuationSession(context.User))
        {
            context.Succeed(requirement);
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            Deny(httpContext);
            return;
        }

        if (httpContext.Request.Path.StartsWithSegments("/hubs/saferide"))
        {
            context.Succeed(requirement);
            return;
        }

        var endpoint = httpContext.GetEndpoint();
        var metadata = endpoint?.Metadata.GetMetadata<AllowTripContinuationAttribute>();
        if (metadata is null)
        {
            Deny(httpContext);
            context.Fail();
            return;
        }

        var tripId = ReadRouteLong(httpContext, "tripId");
        var bookingId = ReadRouteLong(httpContext, "bookingId");
        var allowed = await _accessService.IsAllowedAsync(
            context.User,
            metadata.Operation,
            tripId,
            bookingId,
            httpContext.RequestAborted);
        if (allowed)
        {
            context.Succeed(requirement);
            return;
        }

        Deny(httpContext, AuthErrorCodes.TripContinuationTripMismatch);
        context.Fail();
    }

    private static bool IsContinuationSession(ClaimsPrincipal user)
    {
        return string.Equals(
            user.FindFirstValue(AuthClaimTypes.SessionMode),
            AuthSessionModes.TripContinuation,
            StringComparison.Ordinal);
    }

    private static long? ReadRouteLong(HttpContext httpContext, string key)
    {
        return httpContext.Request.RouteValues.TryGetValue(key, out var value)
            && long.TryParse(
                Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed)
            ? parsed
            : null;
    }

    private static void Deny(
        HttpContext? httpContext,
        string code = AuthErrorCodes.TripContinuationNotAllowed)
    {
        if (httpContext is null)
        {
            return;
        }

        httpContext.Items["AuthErrorCode"] = code;
        httpContext.Items["AuthErrorDetail"] =
            "Phiên tiếp tục chuyến đi không được phép truy cập tài nguyên này.";
    }
}
