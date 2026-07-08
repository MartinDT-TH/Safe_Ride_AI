using System.Collections.Concurrent;

namespace SafeRide.Infrastructure.Redis;

public sealed class InMemoryRedisService : IRedisService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, GeoEntry>> _geoEntries = new();
    private readonly ConcurrentDictionary<long, TripTrackingState> _tripTracking = new();
    private readonly object _sync = new();

    public Task SetAsync(string key, string value, TimeSpan expiration)
    {
        lock (_sync)
        {
            _entries[key] = new CacheEntry(value, GetExpiration(expiration));
            return Task.CompletedTask;
        }
    }

    public Task<bool> SetIfNotExistsAsync(
        string key,
        string value,
        TimeSpan expiration)
    {
        lock (_sync)
        {
            if (GetValue(key) is not null)
            {
                return Task.FromResult(false);
            }

            _entries[key] = new CacheEntry(value, GetExpiration(expiration));
            return Task.FromResult(true);
        }
    }

    public Task<bool> TryAcquireDistributedLockAsync(
        string key,
        string value,
        TimeSpan expiration)
    {
        return SetIfNotExistsAsync(key, value, expiration);
    }

    public Task<string?> GetAsync(string key)
    {
        lock (_sync)
        {
            return Task.FromResult(GetValue(key));
        }
    }

    public Task<IReadOnlyDictionary<string, string?>> GetManyAsync(
        IReadOnlyCollection<string> keys)
    {
        lock (_sync)
        {
            return Task.FromResult<IReadOnlyDictionary<string, string?>>(
                keys
                    .Distinct(StringComparer.Ordinal)
                    .ToDictionary(key => key, GetValue));
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

    public Task GeoAddAsync(
        string key,
        double longitude,
        double latitude,
        string member)
    {
        var members = _geoEntries.GetOrAdd(
            key,
            _ => new ConcurrentDictionary<string, GeoEntry>());
        members[member] = new GeoEntry(longitude, latitude);
        return Task.CompletedTask;
    }

    public Task GeoRemoveAsync(
        string key,
        string member,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_geoEntries.TryGetValue(key, out var members))
        {
            members.TryRemove(member, out _);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GeoRadiusAsync(
        string key,
        double longitude,
        double latitude,
        double radiusKm,
        int count)
    {
        if (!_geoEntries.TryGetValue(key, out var members))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var results = members
            .Select(x => new
            {
                Member = x.Key,
                DistanceKm = CalculateDistanceKm(
                    latitude,
                    longitude,
                    x.Value.Latitude,
                    x.Value.Longitude)
            })
            .Where(x => x.DistanceKm <= radiusKm)
            .OrderBy(x => x.DistanceKm)
            .Take(count)
            .Select(x => x.Member)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(results);
    }

    private static double CalculateDistanceKm(
        double latitude1,
        double longitude1,
        double latitude2,
        double longitude2)
    {
        const double earthRadiusKm = 6371.0;
        var dLat = DegreesToRadians(latitude2 - latitude1);
        var dLon = DegreesToRadians(longitude2 - longitude1);
        var lat1 = DegreesToRadians(latitude1);
        var lat2 = DegreesToRadians(latitude2);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
            + Math.Cos(lat1) * Math.Cos(lat2)
            * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) =>
        degrees * Math.PI / 180;

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

    public Task<TripTrackingUpdateResult> RecordTripTrackingPointAsync(
        TripTrackingPoint point,
        TripTrackingWriteOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            RemoveExpiredTripTracking(point.TripId);
            var state = _tripTracking.GetOrAdd(
                point.TripId,
                _ => new TripTrackingState());
            state.ExpiresAt = GetExpiration(options.Ttl);

            if (point.AccuracyMeters is > 0
                && point.AccuracyMeters > options.MaxAccuracyMeters)
            {
                return Task.FromResult(Reject(state, "low_accuracy"));
            }

            var segmentMeters = 0d;
            if (state.LastAcceptedPoint is not null)
            {
                var validationError = ValidateSegment(
                    state.LastAcceptedPoint,
                    point,
                    options,
                    out segmentMeters);
                if (validationError is not null)
                {
                    return Task.FromResult(Reject(state, validationError));
                }
            }

            state.DistanceMeters += segmentMeters;
            state.FirstAcceptedPoint ??= point;
            state.TrackingStartedAtUtc ??= point.ServerTimestampUtc;
            state.LastAcceptedPoint = point;
            state.LastUpdatedAtUtc = point.ServerTimestampUtc;
            state.AcceptedCount++;

            var appendPath = state.LastPathPoint is null
                || CalculateDistanceKm(
                    state.LastPathPoint.Latitude,
                    state.LastPathPoint.Longitude,
                    point.Latitude,
                    point.Longitude) * 1000d >= options.PathSampleDistanceMeters
                || (point.EffectiveTimestampUnixMs - state.LastPathPoint.EffectiveTimestampUnixMs) / 1000d
                    >= options.PathSampleIntervalSeconds;

            if (appendPath)
            {
                state.PathPoints.Add(point);
                state.LastPathPoint = point;
                if (state.PathPoints.Count > options.MaxPathPoints)
                {
                    state.PathPoints.RemoveRange(
                        0,
                        state.PathPoints.Count - options.MaxPathPoints);
                }
            }

            return Task.FromResult(new TripTrackingUpdateResult(
                true,
                appendPath,
                segmentMeters,
                state.DistanceMeters,
                "accepted"));
        }
    }

    public Task<TripTrackingSnapshot> GetTripTrackingSnapshotAsync(
        long tripId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            RemoveExpiredTripTracking(tripId);
            if (!_tripTracking.TryGetValue(tripId, out var state))
            {
                return Task.FromResult(new TripTrackingSnapshot(
                    [],
                    0,
                    null,
                    null,
                    null,
                    null));
            }

            return Task.FromResult(new TripTrackingSnapshot(
                state.PathPoints.ToList(),
                state.DistanceMeters,
                state.FirstAcceptedPoint,
                state.LastAcceptedPoint,
                state.TrackingStartedAtUtc,
                state.LastUpdatedAtUtc));
        }
    }

    public Task RemoveTripTrackingAsync(
        long tripId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            _tripTracking.TryRemove(tripId, out _);
            foreach (var key in RedisKeys.TripTrackingKeys(tripId))
            {
                _entries.TryRemove(key, out _);
            }

            return Task.CompletedTask;
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

    private static TripTrackingUpdateResult Reject(
        TripTrackingState state,
        string reason)
    {
        state.RejectedCount++;
        return new TripTrackingUpdateResult(
            false,
            false,
            0,
            state.DistanceMeters,
            reason);
    }

    private static string? ValidateSegment(
        TripTrackingPoint previous,
        TripTrackingPoint current,
        TripTrackingWriteOptions options,
        out double segmentMeters)
    {
        segmentMeters = 0;
        if (current.Sequence.HasValue
            && previous.Sequence.HasValue
            && current.Sequence.Value <= previous.Sequence.Value)
        {
            return "old_sequence";
        }

        var elapsedSeconds =
            (current.EffectiveTimestampUnixMs - previous.EffectiveTimestampUnixMs) / 1000d;
        if (elapsedSeconds <= 0)
        {
            return "old_timestamp";
        }

        segmentMeters = CalculateDistanceKm(
            previous.Latitude,
            previous.Longitude,
            current.Latitude,
            current.Longitude) * 1000d;
        if (segmentMeters < options.JitterThresholdMeters)
        {
            return "jitter";
        }

        var inferredSpeedKmh = segmentMeters / elapsedSeconds * 3.6d;
        if (inferredSpeedKmh > options.MaxInferredSpeedKmh)
        {
            return "gps_jump";
        }

        if (current.SpeedMetersPerSecond is > 0
            && current.SpeedMetersPerSecond.Value * 3.6d > options.MaxInferredSpeedKmh)
        {
            return "reported_speed";
        }

        return null;
    }

    private void RemoveExpiredTripTracking(long tripId)
    {
        if (_tripTracking.TryGetValue(tripId, out var state)
            && state.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _tripTracking.TryRemove(tripId, out _);
        }
    }

    private static DateTimeOffset GetExpiration(TimeSpan expiration)
    {
        return DateTimeOffset.UtcNow.Add(expiration);
    }

    private sealed record CacheEntry(string Value, DateTimeOffset ExpiresAt);

    private sealed record GeoEntry(double Longitude, double Latitude);

    private sealed class TripTrackingState
    {
        public List<TripTrackingPoint> PathPoints { get; } = [];
        public double DistanceMeters { get; set; }
        public TripTrackingPoint? FirstAcceptedPoint { get; set; }
        public TripTrackingPoint? LastAcceptedPoint { get; set; }
        public TripTrackingPoint? LastPathPoint { get; set; }
        public DateTime? TrackingStartedAtUtc { get; set; }
        public DateTime? LastUpdatedAtUtc { get; set; }
        public int AcceptedCount { get; set; }
        public int RejectedCount { get; set; }
        public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.MaxValue;
    }
}
