using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SafeRide.API.Filters;

public sealed class AdminStaffUseCaseDenyFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        if (IsAdminBlockedFromStaffOnlyUseCase(context.HttpContext))
        {
            context.Result = new ObjectResult(CreateForbiddenProblem(context.HttpContext))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }

    private static bool IsAdminBlockedFromStaffOnlyUseCase(HttpContext httpContext)
    {
        if (!httpContext.User.IsInRole("Admin"))
        {
            return false;
        }

        var path = httpContext.Request.Path;
        var method = httpContext.Request.Method;

        if (HttpMethods.IsPatch(method)
            && path.StartsWithSegments("/api/admin/drivers")
            && path.Value?.EndsWith("/kyc", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        if (HttpMethods.IsPost(method)
            && path.Equals("/api/admin/notifications", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static ProblemDetails CreateForbiddenProblem(HttpContext httpContext)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = "Ban khong co quyen truy cap chuc nang nay.",
            Instance = httpContext.Request.Path
        };
        problem.Extensions["code"] = "management.staff_only_use_case";
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        return problem;
    }
}
