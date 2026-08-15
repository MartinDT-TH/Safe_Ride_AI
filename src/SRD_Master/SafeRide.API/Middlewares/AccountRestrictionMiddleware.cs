using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using SafeRide.Application.Common.Interfaces;
using SafeRide.Application.Features.AccountBans.DTOs;

namespace SafeRide.API.Middlewares;

public sealed class AccountRestrictionMiddleware
{
    private readonly RequestDelegate _next;

    public AccountRestrictionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IAccountRestrictionService accountRestrictionService)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier),
                out var userId))
        {
            await _next(context);
            return;
        }

        var result = await accountRestrictionService.CheckAccountAccessAsync(
            userId,
            releaseExpiredTemporaryBans: true,
            context.RequestAborted);
        if (result.IsAllowed)
        {
            await _next(context);
            return;
        }

        await WriteProblemAsync(context, result);
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        AccountRestrictionCheckResult result)
    {
        const int statusCode = StatusCodes.Status403Forbidden;
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        if (result.RetryAfterSeconds is > 0)
        {
            context.Response.Headers.RetryAfter = result.RetryAfterSeconds.Value.ToString();
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = string.IsNullOrWhiteSpace(result.Message)
                ? "Tài khoản không được phép truy cập."
                : result.Message,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = result.Code ?? "auth.account_restricted";
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (result.RetryAfterSeconds is > 0)
        {
            problem.Extensions["retryAfterSeconds"] = result.RetryAfterSeconds.Value;
        }

        await context.Response.WriteAsJsonAsync(problem);
    }
}
