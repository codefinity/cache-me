using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CacheMe;

/// <summary>The implementation of <see cref="ICache{T}"/>, registered by AddCacheMe for each value type.</summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class Cache<T> : ICache<T> where T : class
{
    private const int MaxKeyLength = 1024;

    private readonly CachePolicy<T> _policy;
    private readonly ICacheStore _store;
    private readonly IWriteBehindStore? _writeBehindStore;
    private readonly ICacheSerializer _serializer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundWork _backgroundWork;
    private readonly IWriteBehindStatus _writeBehindStatus;
    private readonly ICacheMetrics _metrics;
    private readonly IOptions<CacheMeOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<Cache<T>> _logger;
    private readonly KeyedLoader<T> _loads = new();
    private readonly ConcurrentDictionary<string, byte> _refreshing = new(StringComparer.Ordinal);

    internal Cache(
        CachePolicy<T> policy,
        ICacheStore store,
        IWriteBehindStore? writeBehindStore,
        ICacheSerializer serializer,
        IServiceScopeFactory scopeFactory,
        IBackgroundWork backgroundWork,
        IWriteBehindStatus writeBehindStatus,
        ICacheMetrics metrics,
        IOptions<CacheMeOptions> options,
        TimeProvider timeProvider,
        ILogger<Cache<T>> logger)
    {
        _policy = policy;
        _store = store;
        _writeBehindStore = writeBehindStore;
        _serializer = serializer;
        _scopeFactory = scopeFactory;
        _backgroundWork = backgroundWork;
        _writeBehindStatus = writeBehindStatus;
        _metrics = metrics;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private string ValueType => _policy.SettingsKey;

    /// <inheritdoc />
    public async Task<ReadResult<T>> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        if (_policy.ReadStrategy == ReadStrategy.None)
        {
            throw new InvalidOperationException($"{ValueType} has no read strategy.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var fullKey = FullKey(key);
        CacheEntry? entry = null;
        var storeAvailable = true;
        try
        {
            entry = await _store.GetAsync(fullKey, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!IsCallerCancellation(exception, cancellationToken))
        {
            var error = Error(CacheErrorKind.StoreUnavailable, "The cache store failed.", exception);
            if (_policy.ReadStrategy == ReadStrategy.CacheAside)
            {
                return ReadResult<T>.Failed(error);
            }

            LogHandled("The cache store failed during a read, so the loader is being used", exception);
            storeAvailable = false;
        }

        if (entry is not null && TryDeserialize(entry, out var value))
        {
            _metrics.RecordHit(ValueType);
            StartRefreshIfDue(key, fullKey, entry);
            return ReadResult<T>.Found(value);
        }

        _metrics.RecordMiss(ValueType);
        if (_policy.ReadStrategy == ReadStrategy.CacheAside)
        {
            return ReadResult<T>.NotFound();
        }

        return await _loads
            .RunAsync(key, token => LoadAsync(key, fullKey, storeAvailable, token), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<WriteResult> StoreInCacheAsync(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(value);
        var effectiveTtl = ResolveTtl(ttl);
        if (_policy.ReadStrategy != ReadStrategy.CacheAside)
        {
            throw new InvalidOperationException($"StoreInCacheAsync needs the cache-aside read strategy, which {ValueType} does not use.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var failure = TrySerialize(value, out var bytes);
        if (failure is not null)
        {
            return failure;
        }

        try
        {
            await _store.SetAsync(FullKey(key), bytes, _timeProvider.GetUtcNow(), effectiveTtl, cancellationToken).ConfigureAwait(false);
            return WriteResult.Success();
        }
        catch (Exception exception) when (!IsCallerCancellation(exception, cancellationToken))
        {
            return WriteResult.Failed(Error(CacheErrorKind.StoreUnavailable, "The cache store failed.", exception));
        }
    }

    /// <inheritdoc />
    public async Task<WriteResult> WriteAsync(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        ArgumentNullException.ThrowIfNull(value);
        var effectiveTtl = ResolveTtl(ttl);
        cancellationToken.ThrowIfCancellationRequested();

        return _policy.WriteStrategy switch
        {
            WriteStrategy.WriteThrough => await WriteThroughAsync(key, value, effectiveTtl, cancellationToken).ConfigureAwait(false),
            WriteStrategy.WriteBehind => await WriteBehindAsync(key, value, effectiveTtl, cancellationToken).ConfigureAwait(false),
            WriteStrategy.WriteAround => await WriteAroundAsync(key, value, cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"{ValueType} has no write strategy.")
        };
    }

    /// <inheritdoc />
    public async Task<WriteResult> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        var fullKey = FullKey(key);
        try
        {
            // Cancel first, so a queued write cannot bring the row back.
            if (_writeBehindStore is not null)
            {
                await _writeBehindStore.CancelAsync(fullKey, cancellationToken).ConfigureAwait(false);
            }

            await _store.RemoveAsync(fullKey, cancellationToken).ConfigureAwait(false);
            return WriteResult.Success();
        }
        catch (Exception exception) when (!IsCallerCancellation(exception, cancellationToken))
        {
            return WriteResult.Failed(Error(CacheErrorKind.StoreUnavailable, "The cache store failed.", exception));
        }
    }

    private async Task<ReadResult<T>> LoadAsync(string key, string fullKey, bool storeAvailable, CancellationToken cancellationToken)
    {
        T? value;
        try
        {
            value = await CallLoaderAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return ReadResult<T>.Failed(Error(CacheErrorKind.LoaderFailed, "The loader failed.", exception));
        }

        if (value is null)
        {
            return ReadResult<T>.NotFound();
        }

        if (storeAvailable)
        {
            await StoreLoadedValueAsync(fullKey, value, cancellationToken).ConfigureAwait(false);
        }

        return ReadResult<T>.Found(value);
    }

    private async Task<T?> CallLoaderAsync(string key, CancellationToken cancellationToken)
    {
        var scope = _scopeFactory.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var loader = (ICacheLoader<T>)scope.ServiceProvider.GetRequiredService(_policy.LoaderType!);
            return await loader.LoadAsync(key, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task StoreLoadedValueAsync(string fullKey, T value, CancellationToken cancellationToken)
    {
        if (TrySerialize(value, out var bytes) is not null)
        {
            _logger.LogWarning("A loaded {ValueType} value could not be cached.", ValueType);
            return;
        }

        try
        {
            await _store.SetAsync(fullKey, bytes, _timeProvider.GetUtcNow(), DefaultTtl(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Error(CacheErrorKind.StoreUnavailable, "The cache store failed.", exception);
            LogHandled("A loaded value could not be stored in the cache", exception);
        }
    }

    private void StartRefreshIfDue(string key, string fullKey, CacheEntry entry)
    {
        if (!_policy.RefreshAhead || entry.HasQueuedWrite)
        {
            return;
        }

        var age = _timeProvider.GetUtcNow() - entry.CreatedAt;
        if (age <= entry.Ttl * RefreshThreshold() || !_refreshing.TryAdd(key, 0))
        {
            return;
        }

        _backgroundWork.Run(token => RefreshAsync(key, fullKey, token));
    }

    private async Task RefreshAsync(string key, string fullKey, CancellationToken cancellationToken)
    {
        try
        {
            var value = await CallLoaderAsync(key, cancellationToken).ConfigureAwait(false);
            if (value is not null)
            {
                await StoreLoadedValueAsync(fullKey, value, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The application is shutting down.
        }
        catch (Exception exception)
        {
            Error(CacheErrorKind.LoaderFailed, "The loader failed during a refresh.", exception);
            LogHandled("A refresh failed, so the current value stays until its TTL ends", exception);
        }
        finally
        {
            _refreshing.TryRemove(key, out _);
        }
    }

    private async Task<WriteResult> WriteThroughAsync(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var failure = TrySerialize(value, out var bytes) ?? await CallWriterAsync(key, value, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        var fullKey = FullKey(key);
        try
        {
            await _store.SetAsync(fullKey, bytes, _timeProvider.GetUtcNow(), ttl, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!IsCallerCancellation(exception, cancellationToken))
        {
            Error(CacheErrorKind.StoreUnavailable, "The cache store failed.", exception);
            LogHandled("The database was written but the cache was not, so the old entry is being removed", exception);
            await TryRemoveAsync(fullKey, cancellationToken).ConfigureAwait(false);
        }

        return WriteResult.Success();
    }

    private async Task<WriteResult> WriteBehindAsync(string key, T value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        if (!_writeBehindStatus.IsRunning || _writeBehindStore is null)
        {
            throw new InvalidOperationException("Write-behind requires the .NET generic host, because its queue is processed by a hosted service.");
        }

        var failure = TrySerialize(value, out var bytes);
        if (failure is not null)
        {
            return failure;
        }

        try
        {
            var queued = await _writeBehindStore
                .TryEnqueueAsync(FullKey(key), ValueType, bytes, _timeProvider.GetUtcNow(), ttl, _options.Value.WriteBehind.MaxQueueLength, cancellationToken)
                .ConfigureAwait(false);

            return queued
                ? WriteResult.Success()
                : WriteResult.Failed(Error(CacheErrorKind.QueueFull, "The write-behind queue is full."));
        }
        catch (Exception exception) when (!IsCallerCancellation(exception, cancellationToken))
        {
            return WriteResult.Failed(Error(CacheErrorKind.StoreUnavailable, "The cache store failed.", exception));
        }
    }

    private async Task<WriteResult> WriteAroundAsync(string key, T value, CancellationToken cancellationToken)
    {
        var failure = await CallWriterAsync(key, value, cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        await TryRemoveAsync(FullKey(key), cancellationToken).ConfigureAwait(false);
        return WriteResult.Success();
    }

    private async Task<WriteResult?> CallWriterAsync(string key, T value, CancellationToken cancellationToken)
    {
        try
        {
            var scope = _scopeFactory.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                var writer = (ICacheWriter<T>)scope.ServiceProvider.GetRequiredService(_policy.WriterType!);
                await writer.WriteAsync(key, value, cancellationToken).ConfigureAwait(false);
            }

            return null;
        }
        catch (Exception exception) when (!IsCallerCancellation(exception, cancellationToken))
        {
            return WriteResult.Failed(Error(CacheErrorKind.WriterFailed, "The writer failed.", exception));
        }
    }

    private async Task TryRemoveAsync(string fullKey, CancellationToken cancellationToken)
    {
        try
        {
            await _store.RemoveAsync(fullKey, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!IsCallerCancellation(exception, cancellationToken))
        {
            Error(CacheErrorKind.StoreUnavailable, "The cache store failed.", exception);
            LogHandled("A cache entry could not be removed, so readers may get the old value until its TTL ends", exception);
        }
    }

    private WriteResult? TrySerialize(T value, out byte[] bytes)
    {
        try
        {
            bytes = _serializer.Serialize(value);
        }
        catch (Exception exception)
        {
            bytes = [];
            return WriteResult.Failed(Error(CacheErrorKind.SerializationFailed, "The value could not be serialized.", exception));
        }

        var limit = _options.Value.MaxValueBytes;
        return bytes.Length > limit
            ? WriteResult.Failed(Error(CacheErrorKind.ValueTooLarge, $"The serialized value is {bytes.Length} bytes, over the limit of {limit}."))
            : null;
    }

    private bool TryDeserialize(CacheEntry entry, out T value)
    {
        try
        {
            value = _serializer.Deserialize<T>(entry.Value);
            return true;
        }
        catch (Exception exception)
        {
            value = null!;
            LogHandled("A cached entry could not be read, so it counts as a miss", exception);
            return false;
        }
    }

    private CacheError Error(CacheErrorKind kind, string message, Exception? exception = null)
    {
        _metrics.RecordError(ValueType, kind);
        return new CacheError(kind, message, exception);
    }

    // Exception messages can include keys, which belong at Debug level only.
    private void LogHandled(string message, Exception exception)
    {
        _logger.LogWarning("{ValueType}: {Message} ({ExceptionType}).", ValueType, message, exception.GetType().Name);
        _logger.LogDebug(exception, "{ValueType}: {Message}.", ValueType, message);
    }

    private string FullKey(string key) => _options.Value.KeyPrefix + key;

    private TimeSpan ResolveTtl(TimeSpan? ttl)
    {
        if (ttl is null)
        {
            return DefaultTtl();
        }

        if (ttl.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), ttl, "The TTL must be greater than zero.");
        }

        var limit = PolicySettings()?.MaxTtl ?? _options.Value.MaxTtl;
        if (ttl.Value > limit)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), ttl, $"The TTL {ttl} for {ValueType} must not exceed MaxTtl ({limit}).");
        }

        return ttl.Value;
    }

    private TimeSpan DefaultTtl() =>
        PolicySettings()?.DefaultTtl
        ?? _options.Value.DefaultTtl
        ?? throw new InvalidOperationException("CacheMe:DefaultTtl is required and must be greater than zero.");

    private double RefreshThreshold() => PolicySettings()?.RefreshThreshold ?? _options.Value.RefreshThreshold;

    private CachePolicyOptions? PolicySettings() =>
        _options.Value.Policies.TryGetValue(ValueType, out var settings) ? settings : null;

    private static bool IsCallerCancellation(Exception exception, CancellationToken cancellationToken) =>
        exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

    private static void ValidateKey(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length == 0)
        {
            throw new ArgumentException("The key must not be empty.", nameof(key));
        }

        if (key.Length > MaxKeyLength)
        {
            throw new ArgumentException($"The key must be at most {MaxKeyLength} characters.", nameof(key));
        }
    }
}
