using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using SafeRide.Domain.Entities;

namespace SafeRide.API.Middlewares;

public sealed class ProfileCompletionMiddleware
{
    private const string DefaultFullName = "Người dùng SafeRide";

    private readonly RequestDelegate _next;

    public ProfileCompletionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        UserManager<AspNetUser> userManager)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            IsExemptPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var userIdValue = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
        {
            await _next(context);
            return;
        }

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user == null || IsProfileComplete(user))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            code = "auth.profile_incomplete",
            message = "Vui lòng hoàn thiện thông tin cá nhân trước khi sử dụng chức năng này."
        });
    }

    private static bool IsExemptPath(PathString path)
    {
        return path.StartsWithSegments("/api/auth");
    }

    private static bool IsProfileComplete(AspNetUser user)
    {
        return !string.IsNullOrWhiteSpace(user.FullName) &&
            user.FullName != DefaultFullName &&
            !string.IsNullOrWhiteSpace(user.PhoneNumber) &&
            user.PhoneNumberConfirmed;
    }
}
