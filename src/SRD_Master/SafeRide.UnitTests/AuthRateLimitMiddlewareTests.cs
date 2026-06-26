using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SafeRide.API.Middlewares;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.UnitTests;

public sealed class AuthRateLimitMiddlewareTests
{
    [Fact]
    public async Task SendOtp_FourthRequest_Returns429()
    {
        var redis = new InMemoryRedisService();
        var environment = new DummyHostEnvironment();

        var middleware = new AuthRateLimitMiddleware(
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<AuthRateLimitMiddleware>.Instance,
            environment);

        for (var index = 0; index < 3; index++)
        {
            var allowed = CreateContext();
            await middleware.InvokeAsync(allowed, redis);
            Assert.Equal(StatusCodes.Status200OK, allowed.Response.StatusCode);
        }

        var blocked = CreateContext();
        await middleware.InvokeAsync(blocked, redis);
        Assert.Equal(StatusCodes.Status429TooManyRequests, blocked.Response.StatusCode);
        Assert.True(blocked.Response.Headers.ContainsKey("Retry-After"));
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/auth/send-otp";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class InMemoryRedisService : IRedisService
    {
        private readonly Dictionary<string, long> _counters = new();
        public Task<long> IncrementAsync(string key, TimeSpan expiration)
        {
            _counters[key] = _counters.GetValueOrDefault(key) + 1;
            return Task.FromResult(_counters[key]);
        }
        public Task<string?> GetAsync(string key) => Task.FromResult<string?>(null);
        public Task RemoveAsync(string key) => Task.CompletedTask;
        public Task SetAsync(string key, string value, TimeSpan expiration) => Task.CompletedTask;
        public Task<bool> SetIfNotExistsAsync(string key, string value, TimeSpan expiration) =>
            Task.FromResult(true);
        public Task GeoAddAsync(
            string key,
            double longitude,
            double latitude,
            string member) => Task.CompletedTask;
        public Task GeoRemoveAsync(
            string key,
            string member,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> GeoRadiusAsync(
            string key,
            double longitude,
            double latitude,
            double radiusKm,
            int count) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<OtpVerificationResult> VerifyAndConsumeOtpAsync(
            string otpKey,
            string attemptsKey,
            string expectedHash,
            int maxAttempts) =>
            Task.FromResult(OtpVerificationResult.Missing);
    }

    private sealed class DummyHostEnvironment : Microsoft.Extensions.Hosting.IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "SafeRide.API";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
