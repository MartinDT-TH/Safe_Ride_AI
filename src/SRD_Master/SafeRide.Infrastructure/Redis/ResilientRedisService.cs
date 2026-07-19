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

    public async Task<bool> TryAcquireDistributedLockAsync(
        string key,
        string value,
        TimeSpan expiration)
    {
        if (!CanTryPrimary())
        {
            _logger.LogWarning(
                "Redis primary unavailable; distributed lock {LockKey} was not acquired.",
                key);
            return false;
        }

        try
        {
            var acquired = await _primary.TryAcquireDistributedLockAsync(
                key,
                value,
                expiration);
            MarkPrimaryAvailable();
            return acquired;
        }
        catch (Exception exception)
        {
            MarkPrimaryUnavailable(exception);
            _logger.LogWarning(
                exception,
                "Redis primary unavailable; distributed lock {LockKey} was not acquired.",
                key);
            return false;
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

    public async Task<IReadOnlyDictionary<string, string?>> GetManyAsync(
        IReadOnlyCollection<string> keys)
    {
        var distinctKeys = keys
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (distinctKeys.Count == 0)
        {
            return new Dictionary<string, string?>();
        }

        if (CanTryPrimary())
        {
            try
            {
                var primaryValues = await _primary.GetManyAsync(distinctKeys);
                MarkPrimaryAvailable();
                var missingKeys = distinctKeys
                    .Where(key => !primaryValues.TryGetValue(key, out var value)
                        || value is null)
                    .ToList();
                if (missingKeys.Count == 0)
                {
                    return primaryValues;
                }

                var fallbackValues = await _fallback.GetManyAsync(missingKeys);
                return distinctKeys.ToDictionary(
                    key => key,
                    key => primaryValues.TryGetValue(key, out var primaryValue)
                        && primaryValue is not null
                            ? primaryValue
                            : fallbackValues.GetValueOrDefault(key));
            }
            catch (Exception exception)
            {
                MarkPrimaryUnavailable(exception);
            }
        }

        return await _fallback.GetManyAsync(distinctKeys);
    }

    public async Task RemoveAsync(string key)
    {
        await _fallback.RemoveAsync(key);
        await TryPrimaryAsync(() => _primary.RemoveAsync(key));
    }

    public async Task ExpireAsync(
        string key,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _fallback.ExpireAsync(key, expiration, cancellationToken);
        await TryPrimaryAsync(
            () => _primary.ExpireAsync(key, expiration, cancellationToken));
    }

    public async Task ListRightPushTrimAndExpireAsync(
        string key,
        string value,
        int maxLength,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _fallback.ListRightPushTrimAndExpireAsync(
            key,
            value,
            maxLength,
            expiration,
            cancellationToken);
        await TryPrimaryAsync(
            () => _primary.ListRightPushTrimAndExpireAsync(
                key,
                value,
                maxLength,
                expiration,
                cancellationToken));
    }

    public async Task<IReadOnlyList<string>> ListRangeAsync(
        string key,
        long start = 0,
        long stop = -1,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (CanTryPrimary())
        {
            try
            {
                var primaryValues = await _primary.ListRangeAsync(
                    key,
                    start,
                    stop,
                    cancellationToken);
                MarkPrimaryAvailable();
                if (primaryValues.Count > 0)
                {
                    return primaryValues;
                }
            }
            catch (Exception exception)
            {
                MarkPrimaryUnavailable(exception);
            }
        }

        return await _fallback.ListRangeAsync(
            key,
            start,
            stop,
            cancellationToken);
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

    public async Task GeoRemoveAsync(
        string key,
        string member,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _fallback.GeoRemoveAsync(key, member, cancellationToken);
        await TryPrimaryAsync(
            () => _primary.GeoRemoveAsync(key, member, cancellationToken));
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
                return results;
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

            if (result == OtpVerificationResult.Success)
            {
                await VerifyFallbackAsync();
                return OtpVerificationResult.Success;
            }

            var fallbackResult = await VerifyFallbackAsync();
            if (fallbackResult == OtpVerificationResult.Success)
            {
                return OtpVerificationResult.Success;
            }

            return result == OtpVerificationResult.Missing
                ? fallbackResult
                : result;
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

    public async Task<TripTrackingUpdateResult> RecordTripTrackingPointAsync(
        TripTrackingPoint point,
        TripTrackingWriteOptions options,
        CancellationToken cancellationToken = default)
    {
        var fallbackResult = await _fallback.RecordTripTrackingPointAsync(
            point,
            options,
            cancellationToken);
        if (!CanTryPrimary())
        {
            return fallbackResult;
        }

        try
        {
            var primaryResult = await _primary.RecordTripTrackingPointAsync(
                point,
                options,
                cancellationToken);
            MarkPrimaryAvailable();
            return primaryResult;
        }
        catch (Exception exception)
        {
            MarkPrimaryUnavailable(exception);
            return fallbackResult;
        }
    }

    public async Task<TripTrackingSnapshot> GetTripTrackingSnapshotAsync(
        long tripId,
        CancellationToken cancellationToken = default)
    {
        if (CanTryPrimary())
        {
            try
            {
                var primarySnapshot = await _primary.GetTripTrackingSnapshotAsync(
                    tripId,
                    cancellationToken);
                MarkPrimaryAvailable();
                if (HasSnapshot(primarySnapshot))
                {
                    return primarySnapshot;
                }
            }
            catch (Exception exception)
            {
                MarkPrimaryUnavailable(exception);
            }
        }

        return await _fallback.GetTripTrackingSnapshotAsync(
            tripId,
            cancellationToken);
    }

    public async Task RemoveTripTrackingAsync(
        long tripId,
        CancellationToken cancellationToken = default)
    {
        await _fallback.RemoveTripTrackingAsync(tripId, cancellationToken);
        await TryPrimaryAsync(
            () => _primary.RemoveTripTrackingAsync(tripId, cancellationToken));
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

    private static bool HasSnapshot(TripTrackingSnapshot snapshot)
    {
        return snapshot.DistanceMeters > 0
            || snapshot.PathPoints.Count > 0
            || snapshot.FirstAcceptedPoint is not null
            || snapshot.LastAcceptedPoint is not null;
    }
}
