using Microsoft.AspNetCore.Mvc;
using SafeRide.Infrastructure.Redis;
using System.Collections.Concurrent;

namespace SafeRide.API.Middlewares;

public sealed class AuthRateLimitMiddleware
{
    private static readonly ConcurrentDictionary<string, LocalCounter> LocalCounters = new();
    private static readonly IReadOnlyDictionary<string, int> Limits =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["/api/auth/send-otp"] = 3,
            ["/api/auth/verify-otp"] = 10,
            ["/api/auth/refresh-token"] = 5,
            ["/api/auth/logout"] = 10,
            ["/api/auth/google-login"] = 10,
            ["/api/auth/demo-login"] = 3
        };

    private readonly RequestDelegate _next;
    private readonly ILogger<AuthRateLimitMiddleware> _logger;

    public AuthRateLimitMiddleware(
        RequestDelegate next,
        ILogger<AuthRateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IRedisService redis)
    {
        if (!HttpMethods.IsPost(context.Request.Method)
            || !Limits.TryGetValue(context.Request.Path, out var limit))
        {
            await _next(context);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var windowStart = new DateTimeOffset(
            now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero);
        var retryAfter = Math.Max(
            1,
            (int)Math.Ceiling((windowStart.AddMinutes(1) - now).TotalSeconds));
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key = $"rate:auth:{context.Request.Path.Value}:{clientIp}:{windowStart:yyyyMMddHHmm}";

        long count;
        try
        {
            count = await redis.IncrementAsync(key, TimeSpan.FromSeconds(retryAfter + 5));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Redis unavailable for rate limiting; using local memory.");
            count = IncrementLocal(key, windowStart.AddMinutes(1));
        }

        if (count <= limit)
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/problem+json";
        context.Response.Headers.RetryAfter = retryAfter.ToString();
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = "Bạn đã gửi quá nhiều yêu cầu xác thực. Vui lòng thử lại sau.",
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = "auth.rate_limit_exceeded";
        problem.Extensions["traceId"] = context.TraceIdentifier;
        await context.Response.WriteAsJsonAsync(problem);
    }

    private static long IncrementLocal(string key, DateTimeOffset expiresAt)
    {
        var counter = LocalCounters.AddOrUpdate(
            key,
            _ => new LocalCounter(1, expiresAt),
            (_, current) => current.ExpiresAt <= DateTimeOffset.UtcNow
                ? new LocalCounter(1, expiresAt)
                : current with { Count = current.Count + 1 });
        return counter.Count;
    }

    private sealed record LocalCounter(long Count, DateTimeOffset ExpiresAt);
}