namespace CacheMe;

/// <summary>
/// Application code that reads one value from the database. A new instance is resolved in a new
/// DI scope for each use, so it does not need to be thread-safe.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public interface ICacheLoader<T> where T : class
{
    /// <summary>
    /// Reads one value from the database. Runs on the caller's path for a miss and in the background for a refresh.
    /// Any exception counts as a failed load, and the library does not retry.
    /// </summary>
    /// <param name="key">The caller's key without the prefix.</param>
    /// <param name="cancellationToken">Cancelled at shutdown or when the shared load is abandoned.</param>
    /// <returns>The value, or null if it does not exist.</returns>
    Task<T?> LoadAsync(string key, CancellationToken cancellationToken);
}
