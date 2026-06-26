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
    public async Task GeoRemoveAsync_RemovesMemberFromRadiusResults()
    {
        var redis = new InMemoryRedisService();
        await redis.GeoAddAsync("geo", 108.0, 16.0, "driver-1");
        await redis.GeoAddAsync("geo", 108.001, 16.001, "driver-2");

        await redis.GeoRemoveAsync("geo", "driver-1");

        var results = await redis.GeoRadiusAsync("geo", 108.0, 16.0, 5, 10);
        Assert.DoesNotContain("driver-1", results);
        Assert.Contains("driver-2", results);
    }

    [Fact]
    public async Task GeoRemoveAsync_MissingMemberIsNoOp()
    {
        var redis = new InMemoryRedisService();
        await redis.GeoAddAsync("geo", 108.0, 16.0, "driver-1");

        await redis.GeoRemoveAsync("geo", "missing-driver");

        var results = await redis.GeoRadiusAsync("geo", 108.0, 16.0, 5, 10);
        Assert.Contains("driver-1", results);
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
