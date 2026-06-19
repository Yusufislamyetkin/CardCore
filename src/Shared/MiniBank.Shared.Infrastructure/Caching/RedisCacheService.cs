using System.Text.Json;
using StackExchange.Redis;

namespace MiniBank.Shared.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private IDatabase Db => _redis.GetDatabase();

    public RedisCacheService(IConnectionMultiplexer redis) { _redis = redis; }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        var value = await Db.StringGetAsync(key);
        return value.HasValue ? JsonSerializer.Deserialize<T>(value!) : null;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class
        => await Db.StringSetAsync(key, JsonSerializer.Serialize(value), expiry);

    public async Task RemoveAsync(string key, CancellationToken ct = default)
        => await Db.KeyDeleteAsync(key);

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
        => await Db.KeyExistsAsync(key);

    public async Task<long> DecrementIfSufficientAsync(string key, long amount, CancellationToken ct = default)
    {
        const string lua = """
            local current = tonumber(redis.call('GET', KEYS[1]) or '0')
            if current >= tonumber(ARGV[1]) then
                return redis.call('DECRBY', KEYS[1], ARGV[1])
            else
                return -1
            end
            """;
        var result = await Db.ScriptEvaluateAsync(lua, [(RedisKey)key], [(RedisValue)amount]);
        return (long)result;
    }
}
