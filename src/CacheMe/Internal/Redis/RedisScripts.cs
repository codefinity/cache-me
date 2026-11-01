namespace CacheMe;

/// <summary>
/// Lua scripts for the steps that must be atomic, because many instances share the queue. A record in the
/// writes hash starts with version|attempts|, followed by data only C# reads. See RedisCacheEntryFormat.
/// </summary>
internal static class RedisScripts
{
    private const string ReadVersion = """
        local function version(record)
          return string.sub(record, 1, string.find(record, '|', 1, true) - 1)
        end

        """;

    /// <summary>ARGV: INFO section. Returns the section's text.</summary>
    public const string Info = """
        return redis.call('INFO', ARGV[1])
        """;

    /// <summary>KEYS: entry, writes. Returns the entry and whether a write is queued.</summary>
    public const string Get = """
        return { redis.call('GET', KEYS[1]), redis.call('HEXISTS', KEYS[2], KEYS[1]) }
        """;

    /// <summary>KEYS: entry, writes. ARGV: entry bytes, TTL in ms. Does nothing if a write is queued.</summary>
    public const string Set = """
        if redis.call('HEXISTS', KEYS[2], KEYS[1]) == 1 then
          return 0
        end
        redis.call('SET', KEYS[1], ARGV[1], 'PX', ARGV[2])
        return 1
        """;

    /// <summary>
    /// KEYS: entry, writes, new, held. ARGV: entry bytes, record, queued time, queue limit.
    /// Returns 0 if the queue is full and the key is not already queued.
    /// </summary>
    public const string Enqueue = """
        if redis.call('HEXISTS', KEYS[2], KEYS[1]) == 0 and redis.call('HLEN', KEYS[2]) >= tonumber(ARGV[4]) then
          return 0
        end
        redis.call('SET', KEYS[1], ARGV[1])
        redis.call('HSET', KEYS[2], KEYS[1], ARGV[2])
        if not redis.call('ZSCORE', KEYS[4], KEYS[1]) and not redis.call('ZSCORE', KEYS[3], KEYS[1]) then
          redis.call('ZADD', KEYS[3], ARGV[3], KEYS[1])
        end
        return 1
        """;

    /// <summary>
    /// KEYS: writes, new, held. ARGV: most to claim, queued-before time, now, claim end time.
    /// Returns key, record, key, record, and so on.
    /// </summary>
    public const string Claim = """
        local limit = tonumber(ARGV[1])
        local claimed = {}
        local function take(source, keys)
          for _, key in ipairs(keys) do
            local record = redis.call('HGET', KEYS[1], key)
            redis.call('ZREM', source, key)
            if record then
              redis.call('ZADD', KEYS[3], ARGV[4], key)
              claimed[#claimed + 1] = key
              claimed[#claimed + 1] = record
            end
          end
        end
        take(KEYS[3], redis.call('ZRANGEBYSCORE', KEYS[3], '-inf', ARGV[3], 'LIMIT', 0, limit))
        local remaining = limit - #claimed / 2
        if remaining > 0 then
          take(KEYS[2], redis.call('ZRANGEBYSCORE', KEYS[2], '-inf', ARGV[2], 'LIMIT', 0, remaining))
        end
        return claimed
        """;

    /// <summary>
    /// KEYS: entry, writes, new, held. ARGV: version, TTL in ms, now.
    /// If a newer value replaced this one, the key moves to new, so it is written next.
    /// </summary>
    public const string Complete = ReadVersion + """
        redis.call('ZREM', KEYS[4], KEYS[1])
        local record = redis.call('HGET', KEYS[2], KEYS[1])
        if not record then
          return 0
        end
        if version(record) == ARGV[1] then
          redis.call('HDEL', KEYS[2], KEYS[1])
          redis.call('PEXPIRE', KEYS[1], ARGV[2])
          return 1
        end
        redis.call('ZADD', KEYS[3], ARGV[3], KEYS[1])
        return 0
        """;

    /// <summary>
    /// KEYS: entry, writes, new, held. ARGV: version, next try time, attempts so far + 1.
    /// If a newer value replaced this one, the key moves to new instead.
    /// </summary>
    public const string RetryLater = ReadVersion + """
        local record = redis.call('HGET', KEYS[2], KEYS[1])
        if not record then
          redis.call('ZREM', KEYS[4], KEYS[1])
          return 0
        end
        if version(record) ~= ARGV[1] then
          redis.call('ZREM', KEYS[4], KEYS[1])
          redis.call('ZADD', KEYS[3], ARGV[2], KEYS[1])
          return 0
        end
        local first = string.find(record, '|', 1, true)
        local second = string.find(record, '|', first + 1, true)
        redis.call('HSET', KEYS[2], KEYS[1], string.sub(record, 1, first) .. ARGV[3] .. string.sub(record, second))
        redis.call('ZADD', KEYS[4], ARGV[2], KEYS[1])
        return 1
        """;

    /// <summary>
    /// KEYS: entry, writes, new, held, dead index, dead record.
    /// ARGV: version, dead record bytes, failed time, retention in ms, TTL in ms, now.
    /// </summary>
    public const string MoveToDeadLetter = ReadVersion + """
        redis.call('ZREM', KEYS[4], KEYS[1])
        local record = redis.call('HGET', KEYS[2], KEYS[1])
        if record and version(record) == ARGV[1] then
          redis.call('HDEL', KEYS[2], KEYS[1])
          redis.call('PEXPIRE', KEYS[1], ARGV[5])
        elseif record then
          redis.call('ZADD', KEYS[3], ARGV[6], KEYS[1])
        end
        redis.call('SET', KEYS[6], ARGV[2], 'PX', ARGV[4])
        redis.call('ZADD', KEYS[5], ARGV[3], ARGV[1])
        redis.call('ZREMRANGEBYSCORE', KEYS[5], '-inf', '(' .. (tonumber(ARGV[3]) - tonumber(ARGV[4])))
        return 1
        """;

    /// <summary>KEYS: entry, writes, new, held. Returns whether a write was queued.</summary>
    public const string Cancel = """
        redis.call('ZREM', KEYS[3], KEYS[1])
        redis.call('ZREM', KEYS[4], KEYS[1])
        return redis.call('HDEL', KEYS[2], KEYS[1])
        """;
}
