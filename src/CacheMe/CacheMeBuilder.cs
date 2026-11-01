using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CacheMe;

/// <summary>
/// Used inside AddCacheMe, on one thread at startup. Members return the builder so calls can be chained.
/// Registration errors throw <see cref="InvalidOperationException"/>.
/// </summary>
public sealed class CacheMeBuilder
{
    private readonly IServiceCollection _services;
    private readonly CachePolicyRegistry _registry = new();
    private readonly HashSet<Type> _types = [];
    private bool _storeRegistered;
    private bool _storeSupportsWriteBehind;
    private bool _usesRedis;
    private bool _connectionStringRequired;

    internal CacheMeBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>Overrides settings in code, after JSON.</summary>
    /// <param name="configure">Changes the options.</param>
    /// <returns>This builder.</returns>
    public CacheMeBuilder Configure(Action<CacheMeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        _services.Configure(configure);
        return this;
    }

    /// <summary>Uses Redis with CacheMe:Redis:ConnectionString.</summary>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">A second store is registered.</exception>
    public CacheMeBuilder UseRedis()
    {
        RegisterRedis(connection: null);
        _connectionStringRequired = true;
        return this;
    }

    /// <summary>Uses Redis with this connection string.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">A second store is registered.</exception>
    public CacheMeBuilder UseRedis(string connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        RegisterRedis(connection: null);
        _connectionStringRequired = true;
        return Configure(options => options.Redis.ConnectionString = connectionString);
    }

    /// <summary>Uses the application's existing connection.</summary>
    /// <param name="connection">Returns the connection.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">A second store is registered.</exception>
    public CacheMeBuilder UseRedis(Func<IServiceProvider, IConnectionMultiplexer> connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        RegisterRedis(connection);
        return this;
    }

    /// <summary>Uses another store, registered as a singleton.</summary>
    /// <typeparam name="TStore">The store type.</typeparam>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">
    /// A second store is registered, or a policy uses write-behind and <typeparamref name="TStore"/> is not an <see cref="IWriteBehindStore"/>.
    /// </exception>
    public CacheMeBuilder UseStore<TStore>() where TStore : class, ICacheStore
    {
        ClaimStore(typeof(IWriteBehindStore).IsAssignableFrom(typeof(TStore)));
        _services.AddSingleton<TStore>();
        _services.AddSingleton<ICacheStore>(provider => provider.GetRequiredService<TStore>());
        if (_storeSupportsWriteBehind)
        {
            RegisterWriteBehindStore(provider => (IWriteBehindStore)provider.GetRequiredService<TStore>());
        }

        return this;
    }

    /// <summary>Replaces the serializer.</summary>
    /// <typeparam name="TSerializer">The serializer type.</typeparam>
    /// <returns>This builder.</returns>
    public CacheMeBuilder UseSerializer<TSerializer>() where TSerializer : class, ICacheSerializer
    {
        _services.AddSingleton<ICacheSerializer, TSerializer>();
        return this;
    }

    /// <summary>Sets the dead-letter handler, registered as scoped.</summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <returns>This builder.</returns>
    public CacheMeBuilder UseDeadLetterHandler<THandler>() where THandler : class, IDeadLetterHandler
    {
        _services.TryAddScoped<THandler>();
        _services.AddScoped<IDeadLetterHandler>(provider => provider.GetRequiredService<THandler>());
        return this;
    }

    /// <summary>Registers <see cref="ICache{T}"/> and its policy under the type's short name.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="policy">Sets the type's strategies.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">The type or settings key is already registered.</exception>
    public CacheMeBuilder ForType<T>(Action<CachePolicyBuilder<T>> policy) where T : class =>
        ForType(typeof(T).Name, policy);

    /// <summary>Registers <see cref="ICache{T}"/> and its policy with an explicit settings key.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="settingsKey">The key under CacheMe:Policies.</param>
    /// <param name="policy">Sets the type's strategies.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">The type or settings key is already registered.</exception>
    public CacheMeBuilder ForType<T>(string settingsKey, Action<CachePolicyBuilder<T>> policy) where T : class
    {
        ArgumentNullException.ThrowIfNull(settingsKey);
        ArgumentNullException.ThrowIfNull(policy);
        if (!_types.Add(typeof(T)))
        {
            throw new InvalidOperationException($"{typeof(T).Name} is already registered.");
        }

        if (_registry.TryGet(settingsKey, out _))
        {
            throw new InvalidOperationException($"The settings key {settingsKey} is already used. Pass a settings key to ForType.");
        }

        var builder = new CachePolicyBuilder<T>(_services);
        policy(builder);
        var built = builder.Build(settingsKey);
        _registry.Add(built);

        _services.AddSingleton<ICache<T>>(provider => new Cache<T>(
            built,
            provider.GetRequiredService<ICacheStore>(),
            provider.GetService<IWriteBehindStore>(),
            provider.GetRequiredService<ICacheSerializer>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IBackgroundWork>(),
            provider.GetService<IWriteBehindStatus>() ?? WriteBehindService.NotRunning,
            provider.GetRequiredService<ICacheMetrics>(),
            provider.GetRequiredService<IOptions<CacheMeOptions>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<Cache<T>>>()));

        return this;
    }

    internal void Complete()
    {
        if (!_storeRegistered)
        {
            throw new InvalidOperationException("Register exactly one cache store: UseRedis or UseStore.");
        }

        if (_registry.UsesWriteBehind && !_storeSupportsWriteBehind)
        {
            throw new InvalidOperationException("A policy uses write-behind, but the cache store is not an IWriteBehindStore.");
        }

        _services.AddSingleton<IValidateOptions<CacheMeOptions>>(new CacheMeOptionsValidator(_registry, _connectionStringRequired));
        _services.AddSingleton(_registry);
        _services.TryAddSingleton(TimeProvider.System);
        _services.TryAddSingleton<ICacheSerializer, JsonCacheSerializer>();
        _services.TryAddSingleton<IBackgroundWork, BackgroundWork>();
        _services.TryAddSingleton<ICacheMetrics, CacheMetrics>();

        if (_usesRedis)
        {
            _services.AddSingleton<IHostedService>(provider => new RedisStartupCheck(
                provider.GetRequiredService<RedisCacheStore>(),
                _registry,
                provider.GetRequiredService<ILogger<RedisStartupCheck>>()));
        }

        if (_registry.UsesWriteBehind)
        {
            RegisterWriteBehindService();
        }
    }

    private void RegisterRedis(Func<IServiceProvider, IConnectionMultiplexer>? connection)
    {
        ClaimStore(supportsWriteBehind: true);
        _usesRedis = true;
        _services.AddSingleton(provider => new RedisCacheStore(
            provider.GetRequiredService<IOptions<CacheMeOptions>>(),
            connection is null ? null : () => connection(provider),
            provider.GetRequiredService<ILogger<RedisCacheStore>>()));
        _services.AddSingleton<ICacheStore>(provider => provider.GetRequiredService<RedisCacheStore>());
        RegisterWriteBehindStore(provider => provider.GetRequiredService<RedisCacheStore>());
    }

    private void RegisterWriteBehindStore(Func<IServiceProvider, IWriteBehindStore> store)
    {
        _services.AddSingleton(store);
        _services.AddSingleton<IDeadLetterStore>(store);
    }

    private void RegisterWriteBehindService()
    {
        _services.AddSingleton(provider => new WriteBehindProcessor(
            provider.GetRequiredService<IWriteBehindStore>(),
            _registry,
            provider.GetRequiredService<ICacheSerializer>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ICacheMetrics>(),
            provider.GetRequiredService<IOptions<CacheMeOptions>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<WriteBehindProcessor>>()));
        _services.AddSingleton(provider => new WriteBehindService(
            provider.GetRequiredService<WriteBehindProcessor>(),
            provider.GetRequiredService<ILogger<WriteBehindService>>()));
        _services.AddSingleton<IWriteBehindStatus>(provider => provider.GetRequiredService<WriteBehindService>());
        _services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<WriteBehindService>());
    }

    private void ClaimStore(bool supportsWriteBehind)
    {
        if (_storeRegistered)
        {
            throw new InvalidOperationException("Register exactly one cache store: UseRedis or UseStore.");
        }

        _storeRegistered = true;
        _storeSupportsWriteBehind = supportsWriteBehind;
    }
}
