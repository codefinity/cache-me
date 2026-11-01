using System.Collections.Concurrent;
using CacheMe.Tests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CacheMe.Tests.Integration;

[Collection(RedisCollection.Name), Trait("Category", "Integration")]
public sealed class RedisStartupCheckTests(RedisFixture redis) : IAsyncLifetime
{
    private sealed class RecordingLogger : ILogger<RedisStartupCheck>
    {
        public ConcurrentQueue<string> Warnings { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Enqueue(formatter(state, exception));
            }
        }
    }

    private readonly CancellationToken _token = TestContext.Current.CancellationToken;
    private readonly RecordingLogger _logger = new();

    private IServer Server => redis.Connection.GetServers().Single();

    public async ValueTask InitializeAsync()
    {
        await Server.ConfigSetAsync("maxmemory-policy", "noeviction");
        await Server.ConfigSetAsync("appendonly", "yes");
    }

    public async ValueTask DisposeAsync()
    {
        await Server.ConfigSetAsync("maxmemory-policy", "noeviction");
        await Server.ConfigSetAsync("appendonly", "no");
    }

    private RedisStartupCheck Check(bool writeBehind, IConnectionMultiplexer? connection = null)
    {
        var options = Options.Create(new CacheMeOptions { KeyPrefix = "check:", DefaultTtl = TimeSpan.FromMinutes(5) });
        options.Value.Redis.ConnectionString = redis.ConnectionString;
        var store = new RedisCacheStore(options, connection is null ? null : () => connection, NullLogger<RedisCacheStore>.Instance);
        var registry = new CachePolicyRegistry();
        registry.Add(writeBehind ? Policies.Write(WriteStrategy.WriteBehind) : Policies.CacheAside());
        return new RedisStartupCheck(store, registry, _logger);
    }

    [Fact, Trait("Requirement", "NFR-007")]
    public async Task StartAsync_SafeSettings_LogsNoWarning()
    {
        await Check(writeBehind: true).StartAsync(_token);

        Assert.Empty(_logger.Warnings);
    }

    [Fact, Trait("Requirement", "NFR-007")]
    public async Task StartAsync_AllKeysEviction_WarnsAboutEviction()
    {
        await Server.ConfigSetAsync("maxmemory-policy", "allkeys-lru");

        await Check(writeBehind: true).StartAsync(_token);

        Assert.Contains("eviction", Assert.Single(_logger.Warnings));
    }

    [Fact, Trait("Requirement", "NFR-007")]
    public async Task StartAsync_VolatileEviction_LogsNoWarning()
    {
        await Server.ConfigSetAsync("maxmemory-policy", "volatile-lru");

        await Check(writeBehind: true).StartAsync(_token);

        Assert.Empty(_logger.Warnings);
    }

    [Fact, Trait("Requirement", "NFR-007")]
    public async Task StartAsync_AofOff_WarnsAboutPersistence()
    {
        await Server.ConfigSetAsync("appendonly", "no");

        await Check(writeBehind: true).StartAsync(_token);

        Assert.Contains("AOF", Assert.Single(_logger.Warnings));
    }

    [Fact, Trait("Requirement", "NFR-007")]
    public async Task StartAsync_NoWriteBehindAndUnsafeSettings_LogsNoWarning()
    {
        await Server.ConfigSetAsync("maxmemory-policy", "allkeys-lru");

        await Check(writeBehind: false).StartAsync(_token);

        Assert.Empty(_logger.Warnings);
    }

    [Fact, Trait("Requirement", "NFR-007"), Trait("Requirement", "TD-009")]
    public async Task StartAsync_SuppliedConnectionWithoutAdminMode_StillChecksSettings()
    {
        await using var connection = await ConnectionMultiplexer.ConnectAsync(redis.ConnectionString);
        await Server.ConfigSetAsync("maxmemory-policy", "allkeys-lru");

        await Check(writeBehind: true, connection).StartAsync(_token);

        Assert.Contains("eviction", Assert.Single(_logger.Warnings));
    }
}
