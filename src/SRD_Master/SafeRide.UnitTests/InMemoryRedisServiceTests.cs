using SafeRide.Infrastructure.Redis;

namespace SafeRide.UnitTests;

public sealed class InMemoryRedisServiceTests
{
    [Fact]
    public async Task IncrementAsync_IncrementsWithinExpirationWindow()
    {
        var redis = new InMemoryRedisService();

        var first = await redis.IncrementAsync("counter", TimeSpan.FromMinutes(1));
        var second = await redis.IncrementAsync("counter", TimeSpan.FromMinutes(1));

        Assert.Equal(1, first);
        Assert.Equal(2, second);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullAfterExpiration()
    {
        var redis = new InMemoryRedisService();
        await redis.SetAsync("key", "value", TimeSpan.FromMilliseconds(10));

        await Task.Delay(30);

        Assert.Null(await redis.GetAsync("key"));
    }

    [Fact]
    public async Task VerifyAndConsumeOtpAsync_ConsumesValidOtp()
    {
        var redis = new InMemoryRedisService();
        await redis.SetAsync("otp", "expected", TimeSpan.FromMinutes(1));

        var result = await redis.VerifyAndConsumeOtpAsync(
            "otp",
            "attempts",
            "expected",
            3);

        Assert.Equal(OtpVerificationResult.Success, result);
        Assert.Null(await redis.GetAsync("otp"));
    }

    [Fact]
    public async Task VerifyAndConsumeOtpAsync_RemovesOtpAtAttemptLimit()
    {
        var redis = new InMemoryRedisService();
        await redis.SetAsync("otp", "expected", TimeSpan.FromMinutes(1));

        Assert.Equal(
            OtpVerificationResult.Invalid,
            await redis.VerifyAndConsumeOtpAsync(
                "otp",
                "attempts",
                "wrong",
                2));
        Assert.Equal(
            OtpVerificationResult.AttemptsExceeded,
            await redis.VerifyAndConsumeOtpAsync(
                "otp",
                "attempts",
                "wrong",
                2));
        Assert.Null(await redis.GetAsync("otp"));
    }
}
