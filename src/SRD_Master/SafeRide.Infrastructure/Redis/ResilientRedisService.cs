using Microsoft.Extensions.Logging;

namespace SafeRide.Infrastructure.Redis;

public sealed class ResilientRedisService : IRedisService
{
    private static readonly TimeSpan DefaultCircuitBreakDuration =
        TimeSpan.FromSeconds(30);

    private readonly IRedisService _primary;
    private readonly InMemoryRedisService _fallback;
    private readonly ILogger<ResilientRedisService> _logger;
    private readonly TimeSpan _circuitBreakDuration;
    private long _offlineUntilUtcTicks;

    public ResilientRedisService(
        IRedisService primary,
        InMemoryRedisService fallback,
        ILogger<ResilientRedisService> logger,
        TimeSpan? circuitBreakDuration = null)
    {
        _primary = primary;
        _fallback = fallback;
        _logger = logger;
        _circuitBreakDuration =
            circuitBreakDuration ?? DefaultCircuitBreakDuration;
    }

    public async Task SetAsync(
        string key,
        string value,
        TimeSpan expiration)
    {
        await _fallback.SetAsync(key, value, expiration);
        await TryPrimaryAsync(
            () => _primary.SetAsync(key, value, expiration));
    }

    public async Task<bool> SetIfNotExistsAsync(
        string key,
        string value,
        TimeSpan expiration)
    {
        var fallbackAcquired = await _fallback.SetIfNotExistsAsync(
            key,
            value,
            expiration);
        if (!fallbackAcquired || !CanTryPrimary())
        {
            return fallbackAcquired;
        }

        try
        {
            var primaryAcquired = await _primary.SetIfNotExistsAsync(
                key,
                value,
                expiration);
            MarkPrimaryAvailable();
            if (!primaryAcquired)
            {
                await _fallback.RemoveAsync(key);
            }

            return primaryAcquired;
        }
        catch (Exception exception)
        {
            MarkPrimaryUnavailable(exception);
            return fallbackAcquired;
        }
    }

    public async Task<string?> GetAsync(string key)
    {
        if (CanTryPrimary())
        {
            try
            {
                var value = await _primary.GetAsync(key);
                MarkPrimaryAvailable();
                return value ?? await _fallback.GetAsync(key);
            }
            catch (Exception exception)
            {
                MarkPrimaryUnavailable(exception);
            }
        }

        return await _fallback.GetAsync(key);
    }

    public async Task RemoveAsync(string key)
    {
        await _fallback.RemoveAsync(key);
        await TryPrimaryAsync(() => _primary.RemoveAsync(key));
    }

    public async Task<long> IncrementAsync(
        string key,
        TimeSpan expiration)
    {
        var fallbackCount = await _fallback.IncrementAsync(key, expiration);
        if (!CanTryPrimary())
        {
            return fallbackCount;
        }

        try
        {
            var primaryCount = await _primary.IncrementAsync(key, expiration);
            MarkPrimaryAvailable();
            return Math.Max(primaryCount, fallbackCount);
        }
        catch (Exception exception)
        {
            MarkPrimaryUnavailable(exception);
            return fallbackCount;
        }
    }

    public async Task GeoAddAsync(
        string key,
        double longitude,
        double latitude,
        string member)
    {
        await _fallback.GeoAddAsync(key, longitude, latitude, member);
        await TryPrimaryAsync(
            () => _primary.GeoAddAsync(key, longitude, latitude, member));
    }

    public async Task<IReadOnlyList<string>> GeoRadiusAsync(
        string key,
        double longitude,
        double latitude,
        double radiusKm,
        int count)
    {
        if (CanTryPrimary())
        {
            try
            {
                var results = await _primary.GeoRadiusAsync(
                    key,
                    longitude,
                    latitude,
                    radiusKm,
                    count);
                MarkPrimaryAvailable();
                if (results.Count > 0)
                {
                    return results;
                }
            }
            catch (Exception exception)
            {
                MarkPrimaryUnavailable(exception);
            }
        }

        return await _fallback.GeoRadiusAsync(
            key,
            longitude,
            latitude,
            radiusKm,
            count);
    }

    public async Task<OtpVerificationResult> VerifyAndConsumeOtpAsync(
        string otpKey,
        string attemptsKey,
        string expectedHash,
        int maxAttempts)
    {
        if (!CanTryPrimary())
        {
            return await VerifyFallbackAsync();
        }

        try
        {
            var result = await _primary.VerifyAndConsumeOtpAsync(
                otpKey,
                attemptsKey,
                expectedHash,
                maxAttempts);
            MarkPrimaryAvailable();

            if (result == OtpVerificationResult.Missing)
            {
                return await VerifyFallbackAsync();
            }

            await VerifyFallbackAsync();
            return result;
        }
        catch (Exception exception)
        {
            MarkPrimaryUnavailable(exception);
            return await VerifyFallbackAsync();
        }

        Task<OtpVerificationResult> VerifyFallbackAsync()
        {
            return _fallback.VerifyAndConsumeOtpAsync(
                otpKey,
                attemptsKey,
                expectedHash,
                maxAttempts);
        }
    }

    private async Task TryPrimaryAsync(Func<Task> operation)
    {
        if (!CanTryPrimary())
        {
            return;
        }

        try
        {
            await operation();
            MarkPrimaryAvailable();
        }
        catch (Exception exception)
        {
            MarkPrimaryUnavailable(exception);
        }
    }

    private bool CanTryPrimary()
    {
        return DateTimeOffset.UtcNow.UtcTicks >=
            Interlocked.Read(ref _offlineUntilUtcTicks);
    }

    private void MarkPrimaryAvailable()
    {
        Interlocked.Exchange(ref _offlineUntilUtcTicks, 0);
    }

    private void MarkPrimaryUnavailable(Exception exception)
    {
        var now = DateTimeOffset.UtcNow;
        var previousOfflineUntil = Interlocked.Exchange(
            ref _offlineUntilUtcTicks,
            now.Add(_circuitBreakDuration).UtcTicks);
        if (previousOfflineUntil <= now.UtcTicks)
        {
            _logger.LogWarning(
                exception,
                "Redis unavailable; using in-memory fallback for {DurationSeconds} seconds.",
                _circuitBreakDuration.TotalSeconds);
        }
    }
}
