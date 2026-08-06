using StackExchange.Redis;

namespace SlidingWindow.Api.RateLimiting;

/// <summary>
/// Redis Sorted Set + Lua ile GLOBAL (dağıtık) Sliding Window Log limiter.
/// Tüm replikalar aynı Redis'e baktığından sayaç tek ve ortaktır; böylece
/// çok replikalı ortamda "Sahte Global" oluşmaz.
/// </summary>
public sealed class RedisSlidingWindowLimiter
{
    private readonly IConnectionMultiplexer _redis;
    private readonly int _permitLimit;
    private readonly long _windowMs;

    // Okuma ile yazma arasına başka bir replika giremez yani race condition oluşmaz.
    private const string Script = """
        local clearBefore = tonumber(ARGV[1]) - tonumber(ARGV[2])
        redis.call('ZREMRANGEBYSCORE', KEYS[1], 0, clearBefore)
        local count = redis.call('ZCARD', KEYS[1])
        if count < tonumber(ARGV[3]) then
            redis.call('ZADD', KEYS[1], ARGV[1], ARGV[4])
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
            return 1
        else
            return 0
        end
        """;

    public RedisSlidingWindowLimiter(IConnectionMultiplexer redis, int permitLimit, TimeSpan window)
    {
        _redis = redis;
        _permitLimit = permitLimit;
        _windowMs = (long)window.TotalMilliseconds;
    }

    /// <returns>true = izin verildi, false = limit aşıldı (429).</returns>
    public async Task<bool> TryAcquireAsync(string partitionKey)
    {
        IDatabase db = _redis.GetDatabase();
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // ScriptEvaluateAsync script'i bir kez yükler bu sayede sonraki çağrılarda script'i tekrar 
        // yüklemez ve performans artar.
        RedisResult result = await db.ScriptEvaluateAsync(
            Script,
            new RedisKey[] { $"rate:{partitionKey}" },
            new RedisValue[]
            {
                nowMs,                          // ARGV[1] = now (ms)
                _windowMs,                      // ARGV[2] = window (ms)
                _permitLimit,                   // ARGV[3] = limit
                Guid.NewGuid().ToString()       // ARGV[4] = unique value
            });

        return (long)result == 1;
    }
}
