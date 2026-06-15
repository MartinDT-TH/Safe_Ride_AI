using System.Collections.Concurrent;

namespace SafeRide.Infrastructure.Redis;

public sealed class InMemoryRedisService : IRedisService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();
    private readonly object _sync = new();

    public Task SetAsync(string key, string value, TimeSpan expiration)
    {
        lock (_sync)
        {
            _entries[key] = new CacheEntry(value, GetExpiration(expiration));
            return Task.CompletedTask;
        }
    }

    public Task<string?> GetAsync(string key)
    {
        lock (_sync)
        {
            return Task.FromResult(GetValue(key));
        }
    }

    public Task RemoveAsync(string key)
    {
        lock (_sync)
        {
            _entries.TryRemove(key, out _);
            return Task.CompletedTask;
        }
    }

    public Task<long> IncrementAsync(string key, TimeSpan expiration)
    {
        lock (_sync)
        {
            var currentValue = GetValue(key);
            var expiresAt = currentValue is null
                ? GetExpiration(expiration)
                : _entries[key].ExpiresAt;
            var count = long.TryParse(currentValue, out var current)
                ? current + 1
                : 1;
            _entries[key] = new CacheEntry(
                count.ToString(),
                expiresAt);
            return Task.FromResult(count);
        }
    }

    public Task<OtpVerificationResult> VerifyAndConsumeOtpAsync(
        string otpKey,
        string attemptsKey,
        string expectedHash,
        int maxAttempts)
    {
        lock (_sync)
        {
            var storedHash = GetValue(otpKey);
            if (storedHash is null)
            {
                return Task.FromResult(OtpVerificationResult.Missing);
            }

            var attemptsValue = GetValue(attemptsKey);
            var attempts = int.TryParse(attemptsValue, out var currentAttempts)
                ? currentAttempts
                : 0;
            if (attempts >= maxAttempts)
            {
                RemoveOtp(otpKey, attemptsKey);
                return Task.FromResult(OtpVerificationResult.AttemptsExceeded);
            }

            if (!string.Equals(storedHash, expectedHash, StringComparison.Ordinal))
            {
                attempts++;
                if (attempts >= maxAttempts)
                {
                    RemoveOtp(otpKey, attemptsKey);
                    return Task.FromResult(OtpVerificationResult.AttemptsExceeded);
                }

                _entries[attemptsKey] = new CacheEntry(
                    attempts.ToString(),
                    _entries[otpKey].ExpiresAt);
                return Task.FromResult(OtpVerificationResult.Invalid);
            }

            RemoveOtp(otpKey, attemptsKey);
            return Task.FromResult(OtpVerificationResult.Success);
        }
    }

    private string? GetValue(string key)
    {
        if (!_entries.TryGetValue(key, out var entry))
        {
            return null;
        }

        if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _entries.TryRemove(key, out _);
            return null;
        }

        return entry.Value;
    }

    private void RemoveOtp(string otpKey, string attemptsKey)
    {
        _entries.TryRemove(otpKey, out _);
        _entries.TryRemove(attemptsKey, out _);
    }

    private static DateTimeOffset GetExpiration(TimeSpan expiration)
    {
        return DateTimeOffset.UtcNow.Add(expiration);
    }

    private sealed record CacheEntry(string Value, DateTimeOffset ExpiresAt);
}
