using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace CacheMe.Tests.Support;

public sealed class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
}

/// <summary>A clock that tests move by hand.</summary>
internal sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class Policies
{
    public static CachePolicy<Product> CacheAside(WriteStrategy write = WriteStrategy.None) => new()
    {
        SettingsKey = "Product",
        ReadStrategy = ReadStrategy.CacheAside,
        WriteStrategy = write,
        WriterType = write == WriteStrategy.None ? null : typeof(ICacheWriter<Product>)
    };

    public static CachePolicy<Product> ReadThrough(bool refreshAhead = false) => new()
    {
        SettingsKey = "Product",
        ReadStrategy = ReadStrategy.ReadThrough,
        RefreshAhead = refreshAhead,
        LoaderType = typeof(ICacheLoader<Product>)
    };

    public static CachePolicy<Product> Write(WriteStrategy write) => new()
    {
        SettingsKey = "Product",
        WriteStrategy = write,
        WriterType = typeof(ICacheWriter<Product>)
    };
}

/// <summary>Builds a real <see cref="Cache{T}"/> or <see cref="WriteBehindProcessor"/> with mocked seams.</summary>
internal sealed class CacheHarness
{
    public const string Key = "product:42";
    public const string FullKey = "shop:product:42";

    public static readonly byte[] Bytes = [1, 2, 3];

    public Product Value { get; } = new() { Id = 42, Name = "Keyboard", Price = 49.99m };
    public CancellationToken Token { get; } = TestContext.Current.CancellationToken;

    public Mock<IWriteBehindStore> Store { get; } = new();
    public Mock<ICacheSerializer> Serializer { get; } = new();
    public Mock<ICacheLoader<Product>> Loader { get; } = new();
    public Mock<ICacheWriter<Product>> Writer { get; } = new();
    public Mock<IDeadLetterHandler> DeadLetterHandler { get; } = new();
    public Mock<IBackgroundWork> Background { get; } = new();
    public Mock<IWriteBehindStatus> Status { get; } = new();
    public Mock<ICacheMetrics> Metrics { get; } = new();
    public Mock<ILogger<Cache<Product>>> Logger { get; } = new();
    public Mock<ILogger<WriteBehindProcessor>> ProcessorLogger { get; } = new();
    public TestClock Clock { get; } = new();

    public CacheMeOptions Options { get; } = new()
    {
        KeyPrefix = "shop:",
        DefaultTtl = TimeSpan.FromMinutes(5)
    };

    public CacheHarness()
    {
        Serializer.Setup(s => s.Serialize(It.IsAny<Product>())).Returns(Bytes);
        Serializer.Setup(s => s.Deserialize<Product>(It.IsAny<ReadOnlyMemory<byte>>())).Returns(Value);
        Status.Setup(s => s.IsRunning).Returns(true);
        Store.Setup(s => s.TryEnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        Logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        ProcessorLogger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
    }

    public Cache<Product> Create(CachePolicy<Product> policy) => new(
        policy,
        Store.Object,
        Store.Object,
        Serializer.Object,
        Scopes(),
        Background.Object,
        Status.Object,
        Metrics.Object,
        Microsoft.Extensions.Options.Options.Create(Options),
        Clock,
        Logger.Object);

    public WriteBehindProcessor CreateProcessor()
    {
        var registry = new CachePolicyRegistry();
        registry.Add(Policies.Write(WriteStrategy.WriteBehind));

        return new WriteBehindProcessor(
            Store.Object,
            registry,
            Serializer.Object,
            Scopes(),
            Metrics.Object,
            Microsoft.Extensions.Options.Options.Create(Options),
            Clock,
            ProcessorLogger.Object);
    }

    public CacheEntry Entry(TimeSpan? age = null, TimeSpan? ttl = null, bool queued = false) => new()
    {
        Value = Bytes,
        CreatedAt = Clock.GetUtcNow() - (age ?? TimeSpan.Zero),
        Ttl = ttl ?? TimeSpan.FromMinutes(5),
        HasQueuedWrite = queued
    };

    public QueuedWrite Queued(string key = FullKey, int attempts = 0, string valueType = "Product") => new()
    {
        Key = key,
        ValueType = valueType,
        Value = Bytes,
        Ttl = TimeSpan.FromMinutes(5),
        FirstQueuedAt = Clock.GetUtcNow() - TimeSpan.FromMinutes(1),
        Attempts = attempts,
        Version = "1"
    };

    public void StoreHas(CacheEntry? entry) =>
        Store.Setup(s => s.GetAsync(FullKey, It.IsAny<CancellationToken>())).ReturnsAsync(entry);

    public void StoreGetFails() =>
        Store.Setup(s => s.GetAsync(FullKey, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Redis is down"));

    public void LoaderReturns(Product? value) =>
        Loader.Setup(l => l.LoadAsync(Key, It.IsAny<CancellationToken>())).ReturnsAsync(value);

    public void VerifyStored(TimeSpan ttl, Times times) =>
        Store.Verify(s => s.SetAsync(FullKey, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<DateTimeOffset>(), ttl, It.IsAny<CancellationToken>()), times);

    public void VerifyNotStored() =>
        Store.Verify(s => s.SetAsync(It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);

    public void VerifyLogged(LogLevel level) => VerifyLogged(Logger, level);

    public static void VerifyLogged<TCategory>(Mock<ILogger<TCategory>> logger, LogLevel level) =>
        logger.Verify(
            l => l.Log(level, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.AtLeastOnce);

    // A real provider, so each use really gets a new scope. It holds the mocked loader, writer and handler.
    private IServiceScopeFactory Scopes()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Loader.Object);
        services.AddScoped(_ => Writer.Object);
        services.AddScoped(_ => DeadLetterHandler.Object);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }
}
