using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using SafeRide.Application.Features.Auth;
using SafeRide.Application.Features.Bookings;
using SafeRide.Application.Features.Promotions;
using SafeRide.Application.Features.Ratings;
using SafeRide.Application.Features.Reports;

namespace SafeRide.API.Middlewares;

public sealed class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ApiExceptionMiddleware(
        RequestDelegate next,
        ILogger<ApiExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AuthException exception)
        {
            await WriteProblemAsync(
                context,
                exception.StatusCode,
                exception.Code,
                exception.Message,
                exception.RetryAfterSeconds);
        }
        catch (BookingException exception)
        {
            await WriteProblemAsync(
                context,
                exception.StatusCode,
                exception.Code,
                exception.Message);
        }
        catch (PromotionException exception)
        {
            await WriteProblemAsync(
                context,
                exception.StatusCode,
                exception.Code,
                exception.Message);
        }
        catch (RatingException exception)
        {
            await WriteProblemAsync(
                context,
                exception.StatusCode,
                exception.Code,
                exception.Message);
        }
        catch (ReportException exception)
        {
            await WriteProblemAsync(
                context,
                exception.StatusCode,
                exception.Code,
                exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}.",
                context.Request.Method,
                context.Request.Path);

            var detail = _environment.IsDevelopment() || _environment.EnvironmentName == "Testing"
                ? exception.ToString()
                : "Đã xảy ra lỗi không mong muốn trên hệ thống.";

            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "server.unexpected_error",
                detail);
        }
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string code,
        string detail,
        int? retryAfterSeconds = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        if (retryAfterSeconds is > 0)
        {
            context.Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString();
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (retryAfterSeconds is > 0)
        {
            problem.Extensions["retryAfterSeconds"] = retryAfterSeconds.Value;
        }

        await context.Response.WriteAsJsonAsync(problem);
    }
}
