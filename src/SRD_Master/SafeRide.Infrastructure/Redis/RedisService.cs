using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using System.Globalization;
using System.Text.Json;

namespace SafeRide.Infrastructure.Redis;

public sealed class RedisService : IRedisService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly Lazy<IConnectionMultiplexer> _connection;

    public RedisService(IConfiguration configuration)
    {
        _connection = new Lazy<IConnectionMultiplexer>(() =>
        {
            var connectionString = configuration.GetConnectionString("Redis");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("ConnectionStrings:Redis is not configured.");
            }

            var options = ConfigurationOptions.Parse(connectionString);
            options.AbortOnConnectFail = false;
            options.ConnectRetry = 1;
            options.ConnectTimeout = 10000;
            options.SyncTimeout = 10000;
            options.AsyncTimeout = 10000;
            options.KeepAlive = 30;
            options.Ssl = true;
            return ConnectionMultiplexer.Connect(options);
        });
    }

    private IDatabase Database => _connection.Value.GetDatabase();

    public Task SetAsync(string key, string value, TimeSpan expiration) =>
        Database.StringSetAsync(key, value, expiration);

    public Task<bool> SetIfNotExistsAsync(
        string key,
        string value,
        TimeSpan expiration) =>
        Database.StringSetAsync(
            key,
            value,
            expiration,
            When.NotExists);

    public Task<bool> TryAcquireDistributedLockAsync(
        string key,
        string value,
        TimeSpan expiration) =>
        Database.StringSetAsync(
            key,
            value,
            expiration,
            When.NotExists);

    public async Task<string?> GetAsync(string key)
    {
        var value = await Database.StringGetAsync(key);
        return value.HasValue ? value.ToString() : null;
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

        var redisKeys = distinctKeys
            .Select(key => (RedisKey)key)
            .ToArray();
        var values = await Database.StringGetAsync(redisKeys);
        var result = new Dictionary<string, string?>(distinctKeys.Count);
        var index = 0;
        foreach (var key in distinctKeys)
        {
            var value = values[index++];
            result[key] = value.HasValue ? value.ToString() : null;
        }

        return result;
    }

    public Task RemoveAsync(string key) => Database.KeyDeleteAsync(key);

    public Task ExpireAsync(
        string key,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Database.KeyExpireAsync(key, expiration);
    }

    public async Task ListRightPushTrimAndExpireAsync(
        string key,
        string value,
        int maxLength,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Database.ListRightPushAsync(key, value);
        await Database.ListTrimAsync(key, -maxLength, -1);
        await Database.KeyExpireAsync(key, expiration);
    }

    public async Task<IReadOnlyList<string>> ListRangeAsync(
        string key,
        long start = 0,
        long stop = -1,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var values = await Database.ListRangeAsync(key, start, stop);
        return values
            .Where(value => value.HasValue)
            .Select(value => value.ToString())
            .ToList();
    }

    public async Task<long> IncrementAsync(string key, TimeSpan expiration)
    {
        const string script = """
            local count = redis.call('INCR', KEYS[1])
            if count == 1 then
                redis.call('PEXPIRE', KEYS[1], ARGV[1])
            end
            return count
            """;

        var result = await Database.ScriptEvaluateAsync(
            script,
            new RedisKey[] { key },
            new RedisValue[] { (long)expiration.TotalMilliseconds });
        return (long)result;
    }

    public Task GeoAddAsync(
        string key,
        double longitude,
        double latitude,
        string member) =>
        Database.GeoAddAsync(key, longitude, latitude, member);

    public Task GeoRemoveAsync(
        string key,
        string member,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Database.SortedSetRemoveAsync(key, member);
    }

    public async Task<IReadOnlyList<string>> GeoRadiusAsync(
        string key,
        double longitude,
        double latitude,
        double radiusKm,
        int count)
    {
        var results = await Database.GeoRadiusAsync(
            key,
            longitude,
            latitude,
            radiusKm,
            GeoUnit.Kilometers,
            count);

        return results
            .Select(x => x.Member.ToString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    public async Task<OtpVerificationResult> VerifyAndConsumeOtpAsync(
        string otpKey,
        string attemptsKey,
        string expectedHash,
        int maxAttempts)
    {
        const string script = """
            local stored = redis.call('GET', KEYS[1])
            if not stored then
                return 1
            end

            local attempts = tonumber(redis.call('GET', KEYS[2]) or '0')
            if attempts >= tonumber(ARGV[2]) then
                redis.call('DEL', KEYS[1])
                redis.call('DEL', KEYS[2])
                return 3
            end

            if stored ~= ARGV[1] then
                attempts = redis.call('INCR', KEYS[2])
                if attempts == 1 then
                    local ttl = redis.call('PTTL', KEYS[1])
                    if ttl > 0 then
                        redis.call('PEXPIRE', KEYS[2], ttl)
                    end
                end
                if attempts >= tonumber(ARGV[2]) then
                    redis.call('DEL', KEYS[1])
                    redis.call('DEL', KEYS[2])
                    return 3
                end
                return 2
            end

            redis.call('DEL', KEYS[1])
            redis.call('DEL', KEYS[2])
            return 0
            """;

        var result = await Database.ScriptEvaluateAsync(
            script,
            new RedisKey[] { otpKey, attemptsKey },
            new RedisValue[] { expectedHash, maxAttempts });

        return (OtpVerificationResult)(int)result;
    }

    public async Task<TripTrackingUpdateResult> RecordTripTrackingPointAsync(
        TripTrackingPoint point,
        TripTrackingWriteOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (point.AccuracyMeters is > 0
            && point.AccuracyMeters > options.MaxAccuracyMeters)
        {
            return new TripTrackingUpdateResult(
                false,
                false,
                0,
                0,
                "low_accuracy");
        }

        const string script = """
            local pathKey = KEYS[1]
            local lastAcceptedKey = KEYS[2]
            local lastPathKey = KEYS[3]
            local distanceKey = KEYS[4]
            local metadataKey = KEYS[5]

            local pointJson = ARGV[1]
            local ttlMs = tonumber(ARGV[2])
            local maxPathPoints = tonumber(ARGV[3])
            local jitterMeters = tonumber(ARGV[4])
            local pathDistanceMeters = tonumber(ARGV[5])
            local pathIntervalSeconds = tonumber(ARGV[6])
            local maxSpeedKmh = tonumber(ARGV[7])

            local point = cjson.decode(pointJson)
            local totalDistance = tonumber(redis.call('GET', distanceKey) or '0')

            local function radians(value)
                return value * math.pi / 180
            end

            local function distanceMeters(a, b)
                local earthRadiusMeters = 6371000
                local dLat = radians(b.latitude - a.latitude)
                local dLon = radians(b.longitude - a.longitude)
                local lat1 = radians(a.latitude)
                local lat2 = radians(b.latitude)
                local sinDLat = math.sin(dLat / 2)
                local sinDLon = math.sin(dLon / 2)
                local hav = sinDLat * sinDLat +
                    math.cos(lat1) * math.cos(lat2) * sinDLon * sinDLon
                local c = 2 * math.atan2(math.sqrt(hav), math.sqrt(1 - hav))
                return earthRadiusMeters * c
            end

            local function expireAll()
                redis.call('PEXPIRE', pathKey, ttlMs)
                redis.call('PEXPIRE', lastAcceptedKey, ttlMs)
                redis.call('PEXPIRE', lastPathKey, ttlMs)
                redis.call('PEXPIRE', distanceKey, ttlMs)
                redis.call('PEXPIRE', metadataKey, ttlMs)
            end

            local function reject(reason)
                local metadataJson = redis.call('GET', metadataKey)
                local metadata = metadataJson and cjson.decode(metadataJson) or {}
                metadata.rejectedCount = tonumber(metadata.rejectedCount or '0') + 1
                metadata.lastUpdatedUnixMs = point.serverTimestampUnixMs
                redis.call('SET', metadataKey, cjson.encode(metadata))
                expireAll()
                return { '0', '0', '0', tostring(totalDistance), reason }
            end

            local lastJson = redis.call('GET', lastAcceptedKey)
            local segmentMeters = 0
            if lastJson then
                local last = cjson.decode(lastJson)
                if point.sequence and last.sequence and tonumber(point.sequence) <= tonumber(last.sequence) then
                    return reject('old_sequence')
                end

                local pointTime = tonumber(point.effectiveTimestampUnixMs or '0')
                local lastTime = tonumber(last.effectiveTimestampUnixMs or '0')
                local elapsedSeconds = (pointTime - lastTime) / 1000
                if elapsedSeconds <= 0 then
                    return reject('old_timestamp')
                end

                segmentMeters = distanceMeters(last, point)
                if segmentMeters < jitterMeters then
                    return reject('jitter')
                end

                local inferredSpeedKmh = segmentMeters / elapsedSeconds * 3.6
                if inferredSpeedKmh > maxSpeedKmh then
                    return reject('gps_jump')
                end

                if point.speedMetersPerSecond and tonumber(point.speedMetersPerSecond) * 3.6 > maxSpeedKmh then
                    return reject('reported_speed')
                end
            end

            totalDistance = totalDistance + segmentMeters
            redis.call('SET', distanceKey, tostring(totalDistance))
            redis.call('SET', lastAcceptedKey, pointJson)

            local appendPath = 0
            local lastPathJson = redis.call('GET', lastPathKey)
            if not lastPathJson then
                appendPath = 1
            else
                local lastPath = cjson.decode(lastPathJson)
                local fromPathMeters = distanceMeters(lastPath, point)
                local fromPathSeconds = (tonumber(point.effectiveTimestampUnixMs or '0') - tonumber(lastPath.effectiveTimestampUnixMs or '0')) / 1000
                if fromPathMeters >= pathDistanceMeters or fromPathSeconds >= pathIntervalSeconds then
                    appendPath = 1
                end
            end

            if appendPath == 1 then
                redis.call('RPUSH', pathKey, pointJson)
                redis.call('LTRIM', pathKey, -maxPathPoints, -1)
                redis.call('SET', lastPathKey, pointJson)
            end

            local metadataJson = redis.call('GET', metadataKey)
            local metadata = metadataJson and cjson.decode(metadataJson) or {}
            if not metadata.firstAcceptedPointJson then
                metadata.firstAcceptedPointJson = pointJson
                metadata.trackingStartedUnixMs = point.serverTimestampUnixMs
            end
            metadata.acceptedCount = tonumber(metadata.acceptedCount or '0') + 1
            metadata.lastUpdatedUnixMs = point.serverTimestampUnixMs
            redis.call('SET', metadataKey, cjson.encode(metadata))
            expireAll()

            return { '1', tostring(appendPath), tostring(segmentMeters), tostring(totalDistance), 'accepted' }
            """;

        var result = await Database.ScriptEvaluateAsync(
            script,
            new RedisKey[]
            {
                RedisKeys.TripTrackingPath(point.TripId),
                RedisKeys.TripTrackingLastAcceptedPoint(point.TripId),
                RedisKeys.TripTrackingLastPathPoint(point.TripId),
                RedisKeys.TripTrackingDistanceMeters(point.TripId),
                RedisKeys.TripTrackingMetadata(point.TripId)
            },
            new RedisValue[]
            {
                JsonSerializer.Serialize(point, JsonOptions),
                (long)options.Ttl.TotalMilliseconds,
                options.MaxPathPoints,
                Format(options.JitterThresholdMeters),
                Format(options.PathSampleDistanceMeters),
                options.PathSampleIntervalSeconds,
                Format(options.MaxInferredSpeedKmh)
            });

        var values = (RedisResult[])result;
        return new TripTrackingUpdateResult(
            values[0].ToString() == "1",
            values[1].ToString() == "1",
            ParseDouble(values[2].ToString()),
            ParseDouble(values[3].ToString()),
            values[4].ToString());
    }

    public async Task<TripTrackingSnapshot> GetTripTrackingSnapshotAsync(
        long tripId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pathValues = await Database.ListRangeAsync(
            RedisKeys.TripTrackingPath(tripId));
        var distanceValue = await Database.StringGetAsync(
            RedisKeys.TripTrackingDistanceMeters(tripId));
        var lastAcceptedValue = await Database.StringGetAsync(
            RedisKeys.TripTrackingLastAcceptedPoint(tripId));
        var metadataValue = await Database.StringGetAsync(
            RedisKeys.TripTrackingMetadata(tripId));

        var points = pathValues
            .Where(value => value.HasValue)
            .Select(value => DeserializePoint(value.ToString()))
            .Where(point => point is not null)
            .Select(point => point!)
            .ToList();

        var metadata = metadataValue.HasValue
            ? JsonSerializer.Deserialize<TripTrackingMetadata>(
                metadataValue.ToString(),
                JsonOptions)
            : null;
        var firstAccepted = metadata?.FirstAcceptedPointJson is { Length: > 0 } firstJson
            ? DeserializePoint(firstJson)
            : points.FirstOrDefault();
        var lastAccepted = lastAcceptedValue.HasValue
            ? DeserializePoint(lastAcceptedValue.ToString())
            : points.LastOrDefault();

        return new TripTrackingSnapshot(
            points,
            distanceValue.HasValue ? ParseDouble(distanceValue.ToString()) : 0,
            firstAccepted,
            lastAccepted,
            FromUnixMilliseconds(metadata?.TrackingStartedUnixMs),
            FromUnixMilliseconds(metadata?.LastUpdatedUnixMs));
    }

    public Task RemoveTripTrackingAsync(
        long tripId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keys = RedisKeys.TripTrackingKeys(tripId)
            .Select(key => (RedisKey)key)
            .ToArray();
        return Database.KeyDeleteAsync(keys);
    }

    public void Dispose()
    {
        if (_connection.IsValueCreated)
        {
            _connection.Value.Dispose();
        }
    }

    private static string Format(double value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static double ParseDouble(string value) =>
        double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0d;

    private static TripTrackingPoint? DeserializePoint(string json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<TripTrackingPoint>(json, JsonOptions);
    }

    private static DateTime? FromUnixMilliseconds(long? milliseconds)
    {
        return milliseconds.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds.Value).UtcDateTime
            : null;
    }
}
