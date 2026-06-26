using Hangfire.Dashboard;

namespace SafeRide.API.Filters;

/// <summary>
/// Hangfire Dashboard authorization filter.
/// Allows access only to authenticated users who hold the "Admin" role.
/// </summary>
public sealed class HangfireAdminAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // Must be authenticated.
        if (httpContext.User.Identity is not { IsAuthenticated: true })
        {
            return false;
        }

        // Must be in the Admin role.
        return httpContext.User.IsInRole("Admin");
    }
}
