using System.Collections.Concurrent;
using CacheMe.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace CacheMe.Tests.Integration;

/// <summary>One Redis container shared by all the integration tests.</summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("redis:7.4-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public IConnectionMultiplexer Connection { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        Connection = await ConnectionMultiplexer.ConnectAsync($"{ConnectionString},allowAdmin=true");
    }

    public async ValueTask DisposeAsync()
    {
        await Connection.DisposeAsync();
        await _container.DisposeAsync();
    }

    public async Task ClearAsync()
    {
        foreach (var server in Connection.GetServers())
        {
            await server.FlushAllDatabasesAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class RedisCollection : ICollectionFixture<RedisFixture>
{
    public const string Name = "Redis";
}

internal sealed class RecordingLoader : ICacheLoader<Product>
{
    private int _calls;

    public int Calls => _calls;

    public Product? Value { get; set; } = new() { Id = 42, Name = "Keyboard", Price = 49.99m };

    public Task<Product?> LoadAsync(string key, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        return Task.FromResult(Value);
    }
}

internal sealed class RecordingWriter : ICacheWriter<Product>
{
    public ConcurrentQueue<(string Key, decimal Price)> Writes { get; } = new();

    public bool Fails { get; set; }

    public Func<Task>? BeforeWrite { get; set; }

    public async Task WriteAsync(string key, Product value, CancellationToken cancellationToken)
    {
        if (BeforeWrite is not null)
        {
            await BeforeWrite();
        }

        if (Fails)
        {
            throw new TimeoutException("Database timeout");
        }

        Writes.Enqueue((key, value.Price));
    }
}

internal sealed class RecordingHandler : IDeadLetterHandler
{
    public ConcurrentQueue<DeadLetterWrite> Handled { get; } = new();

    public Task HandleAsync(DeadLetterWrite write, CancellationToken cancellationToken)
    {
        Handled.Enqueue(write);
        return Task.CompletedTask;
    }
}

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<LogLevel> Levels { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(Levels);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<LogLevel> levels) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            levels.Enqueue(logLevel);
    }
}

internal static class TestHosts
{
    public const string Prefix = "shop:";

    public static IHost Create(
        Action<CacheMeOptions> settings,
        Action<CacheMeBuilder> store,
        Action<CachePolicyBuilder<Product>> policy,
        RecordingLoader? loader = null,
        RecordingWriter? writer = null,
        RecordingHandler? handler = null,
        CapturingLoggerProvider? logs = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddLogging();
        if (logs is not null) builder.Logging.AddProvider(logs);
        builder.Services.AddSingleton(loader ?? new RecordingLoader());
        builder.Services.AddSingleton(writer ?? new RecordingWriter());
        builder.Services.AddSingleton(handler ?? new RecordingHandler());

        builder.Services.AddCacheMe(cache =>
        {
            cache.Configure(options =>
            {
                options.KeyPrefix = Prefix;
                options.DefaultTtl = TimeSpan.FromMinutes(5);
                settings(options);
            });
            store(cache);
            cache.UseDeadLetterHandler<RecordingHandler>();
            cache.ForType(policy);
        });

        return builder.Build();
    }

    public static async Task WaitUntil(Func<bool> condition, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(15);
        while (!condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "The condition was not met in time.");
            await Task.Delay(50, cancellationToken);
        }
    }
}
