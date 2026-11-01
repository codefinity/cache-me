namespace CacheMe;

/// <summary>
/// Implement this to use a store other than Redis. The store receives keys with the prefix already added.
/// Stores are singletons and must be safe for concurrent use. Any exception other than
/// <see cref="OperationCanceledException"/> counts as a store failure, and timeouts are the store's responsibility.
/// </summary>
public interface ICacheStore
{
    /// <summary>Reads an entry.</summary>
    /// <param name="key">The full key.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The entry, or null if there is none.</returns>
    Task<CacheEntry?> GetAsync(string key, CancellationToken cancellationToken);

    /// <summary>Stores an entry that expires after <paramref name="ttl"/>.</summary>
    /// <param name="key">The full key.</param>
    /// <param name="value">The serialized value.</param>
    /// <param name="createdAt">Used to work out the refresh threshold.</param>
    /// <param name="ttl">How long the entry can stay in the cache.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the entry is stored.</returns>
    Task SetAsync(string key, ReadOnlyMemory<byte> value, DateTimeOffset createdAt, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>Removes an entry.</summary>
    /// <param name="key">The full key.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Whether an entry existed.</returns>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken);
}
