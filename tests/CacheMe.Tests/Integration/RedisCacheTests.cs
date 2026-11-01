using System.Diagnostics;
using CacheMe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CacheMe.Tests.Integration;

[Collection(RedisCollection.Name), Trait("Category", "Integration")]
public sealed class RedisCacheTests(RedisFixture redis) : IAsyncLifetime
{
    private const string Key = "product:42";
    private const string FullKey = TestHosts.Prefix + Key;

    private static readonly Product Value = new() { Id = 42, Name = "Keyboard", Price = 49.99m };

    private readonly CancellationToken _token = TestContext.Current.CancellationToken;
    private readonly RecordingLoader _loader = new();
    private readonly RecordingWriter _writer = new();
    private readonly RecordingHandler _handler = new();
    private readonly CapturingLoggerProvider _logs = new();

    private IDatabase Database => redis.Connection.GetDatabase();

    public async ValueTask InitializeAsync() => await redis.ClearAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private IHost Host(Action<CachePolicyBuilder<Product>> policy, Action<CacheMeOptions>? settings = null, string? connectionString = null) =>
        TestHosts.Create(
            options => settings?.Invoke(options),
            cache => cache.UseRedis(connectionString ?? redis.ConnectionString),
            policy,
            _loader,
            _writer,
            _handler,
            _logs);

    private static void ReadThrough(CachePolicyBuilder<Product> policy) => policy.ReadThrough<RecordingLoader>();

    private static void WriteBehind(CachePolicyBuilder<Product> policy) => policy.WriteBehind<RecordingWriter>();

    private static void SlowFlush(CacheMeOptions options) => options.WriteBehind.MaxFlushDelay = TimeSpan.FromHours(1);

    private static void FastFlush(CacheMeOptions options) => options.WriteBehind.MaxFlushDelay = TimeSpan.FromMilliseconds(50);

    [Fact, Trait("Requirement", "FR-007"), Trait("Requirement", "FR-033"), Trait("Requirement", "FR-034")]
    public async Task GetAsync_ReadThroughMiss_StoresEntryUnderPrefixedKeyWithTtl()
    {
        using var host = Host(ReadThrough);
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();

        var result = await cache.GetAsync(Key, _token);

        Assert.Equal(49.99m, result.Value!.Price);
        var ttl = await Database.KeyTimeToLiveAsync(FullKey);
        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(5));
    }

    [Fact, Trait("Requirement", "FR-007")]
    public async Task GetAsync_ReadThroughSecondRead_DoesNotCallLoaderAgain()
    {
        using var host = Host(ReadThrough);
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();

        await cache.GetAsync(Key, _token);
        var second = await cache.GetAsync(Key, _token);

        Assert.Equal(ReadStatus.Found, second.Status);
        Assert.Equal(1, _loader.Calls);
    }

    [Fact, Trait("Requirement", "FR-005"), Trait("Requirement", "FR-006"), Trait("Requirement", "FR-035")]
    public async Task StoreInCacheAsync_CacheAside_ValueRoundTrips()
    {
        using var host = Host(policy => policy.CacheAside());
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();

        await cache.StoreInCacheAsync(Key, Value, cancellationToken: _token);
        var result = await cache.GetAsync(Key, _token);

        Assert.Equal(ReadStatus.Found, result.Status);
        Assert.Equal("Keyboard", result.Value!.Name);
        Assert.Equal(49.99m, result.Value.Price);
    }

    [Fact, Trait("Requirement", "FR-016")]
    public async Task GetAsync_EntryCannotBeRead_ReturnsNotFound()
    {
        using var host = Host(policy => policy.CacheAside());
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();
        await cache.StoreInCacheAsync(Key, Value, cancellationToken: _token);

        await Database.StringSetAsync(FullKey, "not valid data");
        var result = await cache.GetAsync(Key, _token);

        Assert.Equal(ReadStatus.NotFound, result.Status);
    }

    [Fact, Trait("Requirement", "FR-008")]
    public async Task GetAsync_EntryPastThreshold_ReloadsInBackground()
    {
        using var host = Host(policy => policy.ReadThrough<RecordingLoader>().RefreshAhead(), options =>
        {
            options.DefaultTtl = TimeSpan.FromSeconds(4);
            options.RefreshThreshold = 0.25;
        });
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();
        await cache.GetAsync(Key, _token);

        await Task.Delay(TimeSpan.FromSeconds(1.5), _token);
        var result = await cache.GetAsync(Key, _token);

        Assert.Equal(ReadStatus.Found, result.Status);
        await TestHosts.WaitUntil(() => _loader.Calls == 2, _token);
    }

    [Fact, Trait("Requirement", "FR-017")]
    public async Task WriteAsync_WriteThrough_WritesDatabaseAndCache()
    {
        using var host = Host(policy => policy.CacheAside().WriteThrough<RecordingWriter>());
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();

        var write = await cache.WriteAsync(Key, Value, cancellationToken: _token);
        var read = await cache.GetAsync(Key, _token);

        Assert.True(write.Succeeded);
        Assert.Single(_writer.Writes);
        Assert.Equal(49.99m, read.Value!.Price);
    }

    [Fact, Trait("Requirement", "FR-030")]
    public async Task WriteAsync_WriteAround_RemovesExistingEntry()
    {
        using var host = Host(policy => policy.CacheAside().WriteAround<RecordingWriter>());
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();
        await cache.StoreInCacheAsync(Key, Value, cancellationToken: _token);

        await cache.WriteAsync(Key, Value, cancellationToken: _token);

        Assert.False(await Database.KeyExistsAsync(FullKey));
        Assert.Single(_writer.Writes);
    }

    [Fact, Trait("Requirement", "FR-032")]
    public async Task RemoveAsync_EntryExists_RemovesIt()
    {
        using var host = Host(policy => policy.CacheAside());
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();
        await cache.StoreInCacheAsync(Key, Value, cancellationToken: _token);

        var result = await cache.RemoveAsync(Key, _token);

        Assert.True(result.Succeeded);
        Assert.False(await Database.KeyExistsAsync(FullKey));
    }

    [Fact, Trait("Requirement", "FR-020"), Trait("Requirement", "FR-022")]
    public async Task WriteAsync_WriteBehind_WritesDatabaseInBackground()
    {
        using var host = Host(WriteBehind, FastFlush);
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();

        var result = await cache.WriteAsync(Key, Value, cancellationToken: _token);

        Assert.True(result.Succeeded);
        await TestHosts.WaitUntil(() => _writer.Writes.Count == 1, _token);
        Assert.Equal((Key, 49.99m), _writer.Writes.Single());
    }

    [Fact, Trait("Requirement", "FR-022")]
    public async Task WriteAsync_FullBatchWaiting_FlushesBeforeMaxDelay()
    {
        using var host = Host(WriteBehind, options =>
        {
            SlowFlush(options);
            options.WriteBehind.MaxBatchSize = 2;
        });
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();

        await cache.WriteAsync("product:1", Value, cancellationToken: _token);
        await cache.WriteAsync("product:2", Value, cancellationToken: _token);

        await TestHosts.WaitUntil(() => _writer.Writes.Count == 2, _token);
    }

    [Fact, Trait("Requirement", "FR-023")]
    public async Task WriteAsync_TwoWritesToOneKeyBeforeFlush_WritesLatestOnce()
    {
        using var host = Host(policy => policy.CacheAside().WriteBehind<RecordingWriter>(), SlowFlush);
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();

        await cache.WriteAsync(Key, new Product { Id = 42, Price = 44.99m }, cancellationToken: _token);
        await cache.WriteAsync(Key, new Product { Id = 42, Price = 39.99m }, cancellationToken: _token);
        var read = await cache.GetAsync(Key, _token);
        await host.StopAsync(_token);

        Assert.Equal(39.99m, read.Value!.Price);
        Assert.Equal((Key, 39.99m), _writer.Writes.Single());
    }

    [Fact, Trait("Requirement", "FR-023"), Trait("Requirement", "FR-024")]
    public async Task WriteAsync_WriteArrivesWhileFlushing_NewestValueEndsUpLast()
    {
        var flushing = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        _writer.BeforeWrite = async () =>
        {
            flushing.TrySetResult();
            await release.Task;
        };
        using var host = Host(WriteBehind, FastFlush);
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();

        await cache.WriteAsync(Key, new Product { Id = 42, Price = 1m }, cancellationToken: _token);
        await flushing.Task.WaitAsync(TimeSpan.FromSeconds(15), _token);
        await cache.WriteAsync(Key, new Product { Id = 42, Price = 2m }, cancellationToken: _token);
        release.SetResult();

        await TestHosts.WaitUntil(() => _writer.Writes.Count == 2, _token);
        Assert.Equal(new[] { 1m, 2m }, _writer.Writes.Select(w => w.Price));
    }

    [Fact, Trait("Requirement", "FR-027")]
    public async Task WriteAsync_WriteQueued_EntryHasNoExpiryUntilCompleted()
    {
        var release = new TaskCompletionSource();
        _writer.BeforeWrite = () => release.Task;
        using var host = Host(WriteBehind, FastFlush);
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();

        await cache.WriteAsync(Key, Value, cancellationToken: _token);
        var whileQueued = await Database.KeyTimeToLiveAsync(FullKey);
        release.SetResult();
        await TestHosts.WaitUntil(() => _writer.Writes.Count == 1, _token);
        await TestHosts.WaitUntil(() => Database.KeyTimeToLive(FullKey) is not null, _token);

        Assert.Null(whileQueued);
    }

    [Fact, Trait("Requirement", "FR-028")]
    public async Task StopAsync_WritesQueued_FlushesThem()
    {
        using var host = Host(WriteBehind, SlowFlush);
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();
        await cache.WriteAsync(Key, Value, cancellationToken: _token);

        await host.StopAsync(_token);

        Assert.Single(_writer.Writes);
    }

    [Fact, Trait("Requirement", "FR-029")]
    public async Task WriteAsync_QueueFull_ReturnsQueueFull()
    {
        using var host = Host(WriteBehind, options =>
        {
            SlowFlush(options);
            options.WriteBehind.MaxBatchSize = 1;
            options.WriteBehind.MaxQueueLength = 1;
        });
        _writer.BeforeWrite = () => Task.Delay(Timeout.Infinite, _token);
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();

        await cache.WriteAsync("product:1", Value, cancellationToken: _token);
        var result = await cache.WriteAsync("product:2", Value, cancellationToken: _token);

        Assert.Equal(CacheErrorKind.QueueFull, result.Error!.Kind);
    }

    [Fact, Trait("Requirement", "FR-029"), Trait("Requirement", "FR-023")]
    public async Task WriteAsync_QueueFullAndKeyAlreadyQueued_ReplacesValue()
    {
        using var host = Host(WriteBehind, options =>
        {
            SlowFlush(options);
            options.WriteBehind.MaxBatchSize = 1;
            options.WriteBehind.MaxQueueLength = 1;
        });
        _writer.BeforeWrite = () => Task.Delay(Timeout.Infinite, _token);
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();

        await cache.WriteAsync(Key, Value, cancellationToken: _token);
        var result = await cache.WriteAsync(Key, Value, cancellationToken: _token);

        Assert.True(result.Succeeded);
    }

    [Fact, Trait("Requirement", "FR-032")]
    public async Task RemoveAsync_WriteQueued_CancelsIt()
    {
        using var host = Host(WriteBehind, SlowFlush);
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();
        await cache.WriteAsync(Key, Value, cancellationToken: _token);

        await cache.RemoveAsync(Key, _token);
        await host.StopAsync(_token);

        Assert.Empty(_writer.Writes);
    }

    [Fact, Trait("Requirement", "FR-025"), Trait("Requirement", "FR-026")]
    public async Task WriteAsync_WriterKeepsFailing_MovesToDeadLetterStoreAndNotifiesHandler()
    {
        _writer.Fails = true;
        using var host = Host(WriteBehind, options =>
        {
            FastFlush(options);
            options.WriteBehind.Retry.MaxAttempts = 2;
            options.WriteBehind.Retry.InitialDelay = TimeSpan.FromMilliseconds(50);
            options.WriteBehind.Retry.MaxDelay = TimeSpan.FromMilliseconds(100);
        });
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();
        var deadLetters = host.Services.GetRequiredService<IDeadLetterStore>();

        await cache.WriteAsync(Key, Value, cancellationToken: _token);
        await TestHosts.WaitUntil(() => !_handler.Handled.IsEmpty, _token);
        var stored = await deadLetters.ListAsync(10, _token);

        var failed = Assert.Single(stored);
        Assert.Equal(FullKey, failed.Key);
        Assert.Equal(2, failed.Attempts);
    }

    [Fact, Trait("Requirement", "FR-026")]
    public async Task DeleteAsync_StoredFailedWrite_RemovesIt()
    {
        _writer.Fails = true;
        using var host = Host(WriteBehind, options =>
        {
            FastFlush(options);
            options.WriteBehind.Retry.MaxAttempts = 1;
        });
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();
        var deadLetters = host.Services.GetRequiredService<IDeadLetterStore>();
        await cache.WriteAsync(Key, Value, cancellationToken: _token);
        await TestHosts.WaitUntil(() => !_handler.Handled.IsEmpty, _token);
        var failed = Assert.Single(await deadLetters.ListAsync(10, _token));

        var deleted = await deadLetters.DeleteAsync(failed.Id, _token);

        Assert.True(deleted);
        Assert.Empty(await deadLetters.ListAsync(10, _token));
    }

    [Fact, Trait("Requirement", "NFR-006")]
    public async Task WriteAsync_TwoInstancesShareQueue_EachWriteHappensOnce()
    {
        using var first = Host(WriteBehind, FastFlush);
        using var second = Host(WriteBehind, FastFlush);
        await first.StartAsync(_token);
        await second.StartAsync(_token);
        var cache = first.Services.GetRequiredService<ICache<Product>>();

        for (var i = 0; i < 50; i++)
        {
            await cache.WriteAsync($"product:{i}", Value, cancellationToken: _token);
        }

        await TestHosts.WaitUntil(() => _writer.Writes.Count >= 50, _token);
        await Task.Delay(500, _token);
        Assert.Equal(50, _writer.Writes.Select(w => w.Key).Distinct().Count());
        Assert.Equal(50, _writer.Writes.Count);
    }

    [Fact, Trait("Requirement", "NFR-007")]
    public async Task WriteAsync_InstanceCrashes_AnotherInstanceFinishesWrite()
    {
        var crashed = Host(WriteBehind, SlowFlush);
        await crashed.StartAsync(_token);
        await crashed.Services.GetRequiredService<ICache<Product>>().WriteAsync(Key, Value, cancellationToken: _token);

        // Dispose without StopAsync, so the shutdown flush does not run.
        crashed.Dispose();
        Assert.Empty(_writer.Writes);
        using var replacement = Host(WriteBehind, FastFlush);
        await replacement.StartAsync(_token);

        await TestHosts.WaitUntil(() => _writer.Writes.Count == 1, _token);
    }

    [Fact, Trait("Requirement", "NFR-007")]
    public async Task StartAsync_EvictionPolicyCanRemoveQueue_LogsWarning()
    {
        var server = redis.Connection.GetServers().Single();
        await server.ConfigSetAsync("maxmemory-policy", "allkeys-lru");
        try
        {
            using var host = Host(WriteBehind);

            await host.StartAsync(_token);

            Assert.Contains(LogLevel.Warning, _logs.Levels);
        }
        finally
        {
            await server.ConfigSetAsync("maxmemory-policy", "noeviction");
        }
    }

    [Fact, Trait("Requirement", "FR-036"), Trait("Requirement", "TD-009")]
    public async Task UseRedis_ExistingConnection_Works()
    {
        using var host = TestHosts.Create(_ => { }, cache => cache.UseRedis(_ => redis.Connection), policy => policy.CacheAside());
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();

        await cache.StoreInCacheAsync(Key, Value, cancellationToken: _token);

        Assert.True(await Database.KeyExistsAsync(FullKey));
    }

    [Fact, Trait("Requirement", "FR-020"), Trait("Requirement", "FR-028")]
    public async Task WriteAsync_NoHostStarted_ThrowsInvalidOperationException()
    {
        using var host = Host(WriteBehind);
        var cache = host.Services.GetRequiredService<ICache<Product>>();

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.WriteAsync(Key, Value, cancellationToken: _token));
    }

    [Fact, Trait("Requirement", "FR-012")]
    public async Task GetAsync_RedisUnreachableReadThrough_FallsBackAndLogsWarning()
    {
        using var host = Host(ReadThrough, o => o.Redis.OperationTimeout = TimeSpan.FromMilliseconds(200), "localhost:1,connectTimeout=100");
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();

        var result = await cache.GetAsync(Key, _token);

        Assert.Equal(ReadStatus.Found, result.Status);
        Assert.Contains(LogLevel.Warning, _logs.Levels);
    }

    [Fact, Trait("Requirement", "FR-012")]
    public async Task GetAsync_RedisUnreachableCacheAside_ReturnsStoreUnavailable()
    {
        using var host = Host(policy => policy.CacheAside(), o => o.Redis.OperationTimeout = TimeSpan.FromMilliseconds(200), "localhost:1,connectTimeout=100");
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();

        var result = await cache.GetAsync(Key, _token);

        Assert.Equal(CacheErrorKind.StoreUnavailable, result.Error!.Kind);
    }

    [Fact, Trait("Requirement", "NFR-003")]
    public async Task GetAsync_RedisUnreachable_FailsWithinOperationTimeout()
    {
        using var host = Host(policy => policy.CacheAside(), o => o.Redis.OperationTimeout = TimeSpan.FromMilliseconds(200), "localhost:1,connectTimeout=100");
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();
        var timer = Stopwatch.StartNew();

        await cache.GetAsync(Key, _token);

        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(2), $"The read took {timer.Elapsed}.");
    }

    [Fact, Trait("Requirement", "FR-012")]
    public async Task StartAsync_RedisUnreachable_DoesNotStopApplicationAndLogsWarning()
    {
        using var host = Host(ReadThrough, connectionString: "localhost:1,connectTimeout=100");

        await host.StartAsync(_token);

        Assert.Contains(LogLevel.Warning, _logs.Levels);
    }

    [Fact, Trait("Requirement", "NFR-005")]
    public async Task GetAsync_ManyConcurrentReaders_AllGetValue()
    {
        using var host = Host(ReadThrough);
        await host.StartAsync(_token);
        var cache = host.Services.GetRequiredService<ICache<Product>>();

        var results = await Task.WhenAll(Enumerable.Range(0, 200).Select(_ => cache.GetAsync(Key, _token)));

        Assert.All(results, r => Assert.Equal(ReadStatus.Found, r.Status));
    }
}
