using Microsoft.Extensions.Logging.Abstractions;
using SafeRide.Infrastructure.Redis;

namespace SafeRide.UnitTests;

public sealed class ResilientRedisServiceTests
{
    [Fact]
    public async Task SetAsync_PrimaryFails_PersistsInFallback()
    {
        var primary = new RedisServiceStub { ShouldFail = true };
        var service = CreateService(primary);

        await service.SetAsync("key", "value", TimeSpan.FromMinutes(1));

        Assert.Equal("value", await service.GetAsync("key"));
        Assert.Equal(1, primary.CallCount);
    }

    [Fact]
    public async Task CircuitOpen_SkipsRepeatedPrimaryCalls()
    {
        var primary = new RedisServiceStub { ShouldFail = true };
        var service = CreateService(primary);

        await service.IncrementAsync("counter", TimeSpan.FromMinutes(1));
        var count = await service.IncrementAsync(
            "counter",
            TimeSpan.FromMinutes(1));

        Assert.Equal(2, count);
        Assert.Equal(1, primary.CallCount);
    }

    [Fact]
    public async Task VerifyOtp_PrimaryFails_UsesMirroredFallback()
    {
        var primary = new RedisServiceStub();
        var service = CreateService(primary);
        await service.SetAsync("otp", "expected", TimeSpan.FromMinutes(1));
        primary.ShouldFail = true;

        var result = await service.VerifyAndConsumeOtpAsync(
            "otp",
            "attempts",
            "expected",
            3);

        Assert.Equal(OtpVerificationResult.Success, result);
    }

    [Fact]
    public async Task IncrementAsync_PrimaryCountLags_ReturnsFallbackCount()
    {
        var primary = new RedisServiceStub { ShouldFail = true };
        var service = new ResilientRedisService(
            primary,
            new InMemoryRedisService(),
            NullLogger<ResilientRedisService>.Instance,
            TimeSpan.Zero);

        await service.IncrementAsync("counter", TimeSpan.FromMinutes(1));
        primary.ShouldFail = false;
        var count = await service.IncrementAsync(
            "counter",
            TimeSpan.FromMinutes(1));

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GeoRemoveAsync_RemovesMemberFromMirroredStores()
    {
        var primary = new RedisServiceStub();
        var service = CreateService(primary);
        await service.GeoAddAsync("geo", 108.0, 16.0, "driver-1");

        await service.GeoRemoveAsync("geo", "driver-1");

        var serviceResults = await service.GeoRadiusAsync("geo", 108.0, 16.0, 5, 10);
        var primaryResults = await primary.GeoRadiusAsync("geo", 108.0, 16.0, 5, 10);
        Assert.DoesNotContain("driver-1", serviceResults);
        Assert.DoesNotContain("driver-1", primaryResults);
    }

    [Fact]
    public async Task GeoRadiusAsync_PrimaryReturnsEmpty_DoesNotUseStaleFallback()
    {
        var primary = new RedisServiceStub();
        var service = CreateService(primary);
        await service.GeoAddAsync("geo", 108.0, 16.0, "driver-1");
        await primary.GeoRemoveAsync("geo", "driver-1");

        var results = await service.GeoRadiusAsync("geo", 108.0, 16.0, 5, 10);

        Assert.Empty(results);
    }

    [Fact]
    public async Task TryAcquireDistributedLockAsync_PrimaryUnavailable_FailsClosed()
    {
        var primary = new RedisServiceStub { ShouldFail = true };
        var service = CreateService(primary);

        var acquired = await service.TryAcquireDistributedLockAsync(
            "lock",
            "owner",
            TimeSpan.FromSeconds(30));

        Assert.False(acquired);
        Assert.Null(await service.GetAsync("lock"));
    }

    private static ResilientRedisService CreateService(
        RedisServiceStub primary)
    {
        return new ResilientRedisService(
            primary,
            new InMemoryRedisService(),
            NullLogger<ResilientRedisService>.Instance,
            TimeSpan.FromMinutes(1));
    }

    private sealed class RedisServiceStub : IRedisService
    {
        private readonly InMemoryRedisService _storage = new();

        public bool ShouldFail { get; set; }
        public int CallCount { get; private set; }

        public Task SetAsync(
            string key,
            string value,
            TimeSpan expiration)
        {
            BeforeCall();
            return _storage.SetAsync(key, value, expiration);
        }

        public Task<bool> SetIfNotExistsAsync(
            string key,
            string value,
            TimeSpan expiration)
        {
            BeforeCall();
            return _storage.SetIfNotExistsAsync(key, value, expiration);
        }

        public Task<bool> TryAcquireDistributedLockAsync(
            string key,
            string value,
            TimeSpan expiration)
        {
            BeforeCall();
            return _storage.TryAcquireDistributedLockAsync(key, value, expiration);
        }

        public Task<string?> GetAsync(string key)
        {
            BeforeCall();
            return _storage.GetAsync(key);
        }

        public Task<IReadOnlyDictionary<string, string?>> GetManyAsync(
            IReadOnlyCollection<string> keys)
        {
            BeforeCall();
            return _storage.GetManyAsync(keys);
        }

        public Task RemoveAsync(string key)
        {
            BeforeCall();
            return _storage.RemoveAsync(key);
        }

        public Task<long> IncrementAsync(
            string key,
            TimeSpan expiration)
        {
            BeforeCall();
            return _storage.IncrementAsync(key, expiration);
        }

        public Task GeoAddAsync(
            string key,
            double longitude,
            double latitude,
            string member)
        {
            BeforeCall();
            return _storage.GeoAddAsync(key, longitude, latitude, member);
        }

        public Task GeoRemoveAsync(
            string key,
            string member,
            CancellationToken cancellationToken = default)
        {
            BeforeCall();
            return _storage.GeoRemoveAsync(key, member, cancellationToken);
        }

        public Task<IReadOnlyList<string>> GeoRadiusAsync(
            string key,
            double longitude,
            double latitude,
            double radiusKm,
            int count)
        {
            BeforeCall();
            return _storage.GeoRadiusAsync(
                key,
                longitude,
                latitude,
                radiusKm,
                count);
        }

        public Task<OtpVerificationResult> VerifyAndConsumeOtpAsync(
            string otpKey,
            string attemptsKey,
            string expectedHash,
            int maxAttempts)
        {
            BeforeCall();
            return _storage.VerifyAndConsumeOtpAsync(
                otpKey,
                attemptsKey,
                expectedHash,
                maxAttempts);
        }

        private void BeforeCall()
        {
            CallCount++;
            if (ShouldFail)
            {
                throw new InvalidOperationException("Redis unavailable.");
            }
        }
    }
}
