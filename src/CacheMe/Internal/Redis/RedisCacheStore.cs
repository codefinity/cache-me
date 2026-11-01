using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CacheMe;

/// <summary>Implements the cache, the write-behind queue and the dead-letter store on Redis.</summary>
internal sealed class RedisCacheStore : IWriteBehindStore, IDisposable
{
    private const int MaxDeadLetterListPasses = 10;

    private readonly IOptions<CacheMeOptions> _options;
    private readonly ILogger<RedisCacheStore> _logger;
    private readonly Lazy<Task<IConnectionMultiplexer>> _connection;
    private readonly bool _ownsConnection;
    private int _disposed;

    public RedisCacheStore(IOptions<CacheMeOptions> options, Func<IConnectionMultiplexer>? suppliedConnection, ILogger<RedisCacheStore> logger)
    {
        _options = options;
        _logger = logger;
        _ownsConnection = suppliedConnection is null;
        _connection = new Lazy<Task<IConnectionMultiplexer>>(suppliedConnection is null
            ? ConnectAsync
            : () => Task.FromResult(suppliedConnection()));
    }

    private string Prefix => _options.Value.KeyPrefix;

    private RedisKey WritesKey => $"{Prefix}__cacheme:writes";

    private RedisKey NewKey => $"{Prefix}__cacheme:new";

    private RedisKey HeldKey => $"{Prefix}__cacheme:held";

    private RedisKey DeadIndexKey => $"{Prefix}__cacheme:dead:index";

    public Task<CacheEntry?> GetAsync(string key, CancellationToken cancellationToken) =>
        RunAsync<CacheEntry?>(async database =>
        {
            var result = await database
                .ScriptEvaluateAsync(RedisScripts.Get, [key, WritesKey])
                .ConfigureAwait(false);
            var parts = (RedisResult[])result!;
            var bytes = (byte[]?)parts[0];
            if (bytes is null)
            {
                return null;
            }

            var entry = RedisCacheEntryFormat.DecodeEntry(bytes, (int)parts[1] == 1);
            if (entry is null)
            {
                _logger.LogWarning("A cached entry is not in the expected format, so it counts as a miss.");
            }

            return entry;
        }, cancellationToken);

    public Task SetAsync(string key, ReadOnlyMemory<byte> value, DateTimeOffset createdAt, TimeSpan ttl, CancellationToken cancellationToken) =>
        RunAsync(async database =>
        {
            var entry = RedisCacheEntryFormat.EncodeEntry(value.Span, createdAt, ttl);
            await database
                .ScriptEvaluateAsync(RedisScripts.Set, [key, WritesKey], [entry, RedisCacheEntryFormat.Milliseconds(ttl)])
                .ConfigureAwait(false);
            return true;
        }, cancellationToken);

    public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken) =>
        RunAsync(database => database.KeyDeleteAsync(key), cancellationToken);

    public Task<bool> TryEnqueueAsync(string key, string valueType, ReadOnlyMemory<byte> value, DateTimeOffset queuedAt, TimeSpan ttl, int maxQueueLength, CancellationToken cancellationToken) =>
        RunAsync(async database =>
        {
            var entry = RedisCacheEntryFormat.EncodeEntry(value.Span, queuedAt, ttl);
            var record = RedisCacheEntryFormat.EncodeWrite(Guid.NewGuid().ToString("N"), 0, ttl, queuedAt, valueType, value.Span);
            var result = await database
                .ScriptEvaluateAsync(
                    RedisScripts.Enqueue,
                    [key, WritesKey, NewKey, HeldKey],
                    [entry, record, queuedAt.ToUnixTimeMilliseconds(), maxQueueLength])
                .ConfigureAwait(false);
            return (int)result == 1;
        }, cancellationToken);

    public Task<IReadOnlyList<QueuedWrite>> ClaimAsync(int maxCount, DateTimeOffset queuedBefore, DateTimeOffset now, TimeSpan claimDuration, CancellationToken cancellationToken) =>
        RunAsync<IReadOnlyList<QueuedWrite>>(async database =>
        {
            var result = await database
                .ScriptEvaluateAsync(
                    RedisScripts.Claim,
                    [WritesKey, NewKey, HeldKey],
                    [maxCount, queuedBefore.ToUnixTimeMilliseconds(), now.ToUnixTimeMilliseconds(), (now + claimDuration).ToUnixTimeMilliseconds()])
                .ConfigureAwait(false);

            var parts = (RedisResult[])result!;
            var writes = new List<QueuedWrite>(parts.Length / 2);
            for (var i = 0; i < parts.Length; i += 2)
            {
                writes.Add(RedisCacheEntryFormat.DecodeWrite((string)parts[i]!, (byte[])parts[i + 1]!));
            }

            return writes;
        }, cancellationToken);

    public Task CompleteAsync(QueuedWrite write, CancellationToken cancellationToken) =>
        RunAsync(async database =>
        {
            await database
                .ScriptEvaluateAsync(
                    RedisScripts.Complete,
                    [write.Key, WritesKey, NewKey, HeldKey],
                    [write.Version, RedisCacheEntryFormat.Milliseconds(write.Ttl), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()])
                .ConfigureAwait(false);
            return true;
        }, cancellationToken);

    public Task RetryLaterAsync(QueuedWrite write, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken) =>
        RunAsync(async database =>
        {
            await database
                .ScriptEvaluateAsync(
                    RedisScripts.RetryLater,
                    [write.Key, WritesKey, NewKey, HeldKey],
                    [write.Version, nextAttemptAt.ToUnixTimeMilliseconds(), write.Attempts + 1])
                .ConfigureAwait(false);
            return true;
        }, cancellationToken);

    public Task MoveToDeadLetterAsync(QueuedWrite write, string failureReason, DateTimeOffset failedAt, TimeSpan retention, CancellationToken cancellationToken) =>
        RunAsync(async database =>
        {
            var deadLetter = JsonSerializer.SerializeToUtf8Bytes(new DeadLetterWrite
            {
                Id = write.Version,
                Key = write.Key,
                ValueType = write.ValueType,
                Value = write.Value,
                Attempts = write.Attempts + 1,
                FirstQueuedAt = write.FirstQueuedAt,
                FailedAt = failedAt,
                FailureReason = failureReason
            });

            await database
                .ScriptEvaluateAsync(
                    RedisScripts.MoveToDeadLetter,
                    [write.Key, WritesKey, NewKey, HeldKey, DeadIndexKey, DeadKey(write.Version)],
                    [write.Version, deadLetter, failedAt.ToUnixTimeMilliseconds(), RedisCacheEntryFormat.Milliseconds(retention), RedisCacheEntryFormat.Milliseconds(write.Ttl), failedAt.ToUnixTimeMilliseconds()])
                .ConfigureAwait(false);
            return true;
        }, cancellationToken);

    public Task<bool> CancelAsync(string key, CancellationToken cancellationToken) =>
        RunAsync(async database =>
        {
            var result = await database
                .ScriptEvaluateAsync(RedisScripts.Cancel, [key, WritesKey, NewKey, HeldKey])
                .ConfigureAwait(false);
            return (int)result == 1;
        }, cancellationToken);

    public Task<long> GetQueueLengthAsync(CancellationToken cancellationToken) =>
        RunAsync(database => database.HashLengthAsync(WritesKey), cancellationToken);

    public Task<IReadOnlyList<DeadLetterWrite>> ListAsync(int maxCount, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCount, 1);

        return RunAsync<IReadOnlyList<DeadLetterWrite>>(async database =>
        {
            var writes = new List<DeadLetterWrite>();

            // Records that Redis has expired leave stale IDs behind, so clear those and read on.
            for (var pass = 0; pass < MaxDeadLetterListPasses && writes.Count < maxCount; pass++)
            {
                var ids = await database
                    .SortedSetRangeByRankAsync(DeadIndexKey, writes.Count, maxCount - 1)
                    .ConfigureAwait(false);
                if (ids.Length == 0)
                {
                    break;
                }

                var records = await database
                    .StringGetAsync(ids.Select(id => DeadKey(id!)).ToArray())
                    .ConfigureAwait(false);

                for (var i = 0; i < ids.Length; i++)
                {
                    if (records[i].IsNull)
                    {
                        await database.SortedSetRemoveAsync(DeadIndexKey, ids[i]).ConfigureAwait(false);
                    }
                    else
                    {
                        writes.Add(JsonSerializer.Deserialize<DeadLetterWrite>((byte[])records[i]!)!);
                    }
                }
            }

            return writes;
        }, cancellationToken);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken) =>
        RunAsync(async database =>
        {
            await database.SortedSetRemoveAsync(DeadIndexKey, id).ConfigureAwait(false);
            return await database.KeyDeleteAsync(DeadKey(id)).ConfigureAwait(false);
        }, cancellationToken);

    /// <summary>Checks that Redis can be reached, within the operation timeout.</summary>
    public Task PingAsync(CancellationToken cancellationToken) =>
        RunAsync(database => database.PingAsync(), cancellationToken);

    /// <summary>Reads the given INFO section from the server, within the operation timeout.</summary>
    public Task<Dictionary<string, string>> ReadInfoAsync(string section, CancellationToken cancellationToken) =>
        RunAsync(async database =>
        {
            // The client refuses a direct INFO unless the connection is in admin mode, which this read-only check
            // should not need, and which a supplied connection may not have. INFO from a script is allowed.
            var result = await database.ScriptEvaluateAsync(RedisScripts.Info, values: [section]).ConfigureAwait(false);
            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in ((string?)result ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = line.IndexOf(':');
                if (separator > 0)
                {
                    settings[line[..separator]] = line[(separator + 1)..];
                }
            }

            return settings;
        }, cancellationToken);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0
            && _ownsConnection
            && _connection.IsValueCreated
            && _connection.Value.IsCompletedSuccessfully)
        {
            _connection.Value.Result.Dispose();
        }
    }

    private RedisKey DeadKey(string id) => $"{Prefix}__cacheme:dead:{id}";

    // The timeout includes waiting for a connection, and also works with a supplied connection.
    private async Task<TResult> RunAsync<TResult>(Func<IDatabase, Task<TResult>> operation, CancellationToken cancellationToken)
    {
        async Task<TResult> Run()
        {
            var connection = await _connection.Value.ConfigureAwait(false);
            return await operation(connection.GetDatabase()).ConfigureAwait(false);
        }

        return await Run()
            .WaitAsync(_options.Value.Redis.OperationTimeout, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<IConnectionMultiplexer> ConnectAsync()
    {
        var redis = _options.Value.Redis;
        var configuration = ConfigurationOptions.Parse(redis.ConnectionString!);
        configuration.AbortOnConnectFail = false;
        var timeout = (int)Math.Ceiling(redis.OperationTimeout.TotalMilliseconds);
        configuration.AsyncTimeout = timeout;
        configuration.SyncTimeout = timeout;
        return await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
    }
}
