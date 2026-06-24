using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace SafeRide.Infrastructure.Redis;

public sealed class RedisService : IRedisService, IDisposable
{
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
            options.ConnectTimeout = 1000;
            options.SyncTimeout = 1000;
            options.AsyncTimeout = 1000;
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

    public async Task<string?> GetAsync(string key)
    {
        var value = await Database.StringGetAsync(key);
        return value.HasValue ? value.ToString() : null;
    }

    public Task RemoveAsync(string key) => Database.KeyDeleteAsync(key);

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

    public void Dispose()
    {
        if (_connection.IsValueCreated)
        {
            _connection.Value.Dispose();
        }
    }
}
