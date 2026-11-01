using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CacheMe;

/// <summary>
/// Used inside ForType, on one thread at startup. Members return the builder. A policy must end up with at least
/// one strategy, at most one read strategy and at most one write strategy, otherwise registration throws
/// <see cref="InvalidOperationException"/>. Any read strategy can be combined with any write strategy.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class CachePolicyBuilder<T> where T : class
{
    private readonly IServiceCollection _services;
    private ReadStrategy _read;
    private WriteStrategy _write;
    private bool _refreshAhead;
    private Type? _loaderType;
    private Type? _writerType;

    internal CachePolicyBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>Read strategy: the repository handles the miss.</summary>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">A read strategy is already set.</exception>
    public CachePolicyBuilder<T> CacheAside()
    {
        SetRead(ReadStrategy.CacheAside);
        return this;
    }

    /// <summary>Read strategy: the library calls the loader on a miss.</summary>
    /// <typeparam name="TLoader">The loader type.</typeparam>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">A read strategy is already set.</exception>
    public CachePolicyBuilder<T> ReadThrough<TLoader>() where TLoader : class, ICacheLoader<T>
    {
        SetRead(ReadStrategy.ReadThrough);
        _services.TryAddScoped<TLoader>();
        _loaderType = typeof(TLoader);
        return this;
    }

    /// <summary>Add-on: reloads in the background past the threshold.</summary>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">No read-through strategy is set.</exception>
    public CachePolicyBuilder<T> RefreshAhead()
    {
        if (_read != ReadStrategy.ReadThrough)
        {
            throw new InvalidOperationException($"{typeof(T).Name} uses RefreshAhead, which requires ReadThrough.");
        }

        _refreshAhead = true;
        return this;
    }

    /// <summary>Write strategy: database, then cache.</summary>
    /// <typeparam name="TWriter">The writer type.</typeparam>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">A write strategy is already set.</exception>
    public CachePolicyBuilder<T> WriteThrough<TWriter>() where TWriter : class, ICacheWriter<T> =>
        SetWrite<TWriter>(WriteStrategy.WriteThrough);

    /// <summary>Write strategy: cache and queue, database later.</summary>
    /// <typeparam name="TWriter">The writer type.</typeparam>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">A write strategy is already set.</exception>
    public CachePolicyBuilder<T> WriteBehind<TWriter>() where TWriter : class, ICacheWriter<T> =>
        SetWrite<TWriter>(WriteStrategy.WriteBehind);

    /// <summary>Write strategy: database, then remove the entry.</summary>
    /// <typeparam name="TWriter">The writer type.</typeparam>
    /// <returns>This builder.</returns>
    /// <exception cref="InvalidOperationException">A write strategy is already set.</exception>
    public CachePolicyBuilder<T> WriteAround<TWriter>() where TWriter : class, ICacheWriter<T> =>
        SetWrite<TWriter>(WriteStrategy.WriteAround);

    internal CachePolicy<T> Build(string settingsKey)
    {
        if (_read == ReadStrategy.None && _write == WriteStrategy.None)
        {
            throw new InvalidOperationException($"{typeof(T).Name} must set one read strategy, one write strategy or both.");
        }

        return new CachePolicy<T>
        {
            SettingsKey = settingsKey,
            ReadStrategy = _read,
            RefreshAhead = _refreshAhead,
            WriteStrategy = _write,
            LoaderType = _loaderType,
            WriterType = _writerType
        };
    }

    private void SetRead(ReadStrategy strategy)
    {
        if (_read != ReadStrategy.None)
        {
            throw new InvalidOperationException($"{typeof(T).Name} must set one read strategy, one write strategy or both.");
        }

        _read = strategy;
    }

    private CachePolicyBuilder<T> SetWrite<TWriter>(WriteStrategy strategy) where TWriter : class, ICacheWriter<T>
    {
        if (_write != WriteStrategy.None)
        {
            throw new InvalidOperationException($"{typeof(T).Name} must set one read strategy, one write strategy or both.");
        }

        _services.TryAddScoped<TWriter>();
        _write = strategy;
        _writerType = typeof(TWriter);
        return this;
    }
}
