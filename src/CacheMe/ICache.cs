namespace CacheMe;

/// <summary>
/// The cache for one value type, injected into a repository. The strategies come from the
/// policy registered for <typeparamref name="T"/>. All members are safe for concurrent use.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public interface ICache<T> where T : class
{
    /// <summary>Reads a value using the type's read strategy.</summary>
    /// <param name="key">The caller's key without the prefix. Not empty, and at most 1,024 characters.</param>
    /// <param name="cancellationToken">Stops the caller's wait. A load shared with other callers, or a background reload, continues.</param>
    /// <returns>
    /// <see cref="ReadStatus.Found"/> with the value, <see cref="ReadStatus.NotFound"/>, or
    /// <see cref="ReadStatus.Failed"/> with a <see cref="CacheError"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or over 1,024 characters.</exception>
    /// <exception cref="InvalidOperationException">The policy has no read strategy.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    Task<ReadResult<T>> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a value the repository has just loaded after a cache-aside miss, without triggering any write strategy.
    /// </summary>
    /// <param name="key">The caller's key without the prefix. Not empty, and at most 1,024 characters.</param>
    /// <param name="value">The value to store. Not null.</param>
    /// <param name="ttl">Greater than zero and at most the effective MaxTtl. Null uses the effective DefaultTtl.</param>
    /// <param name="cancellationToken">Stops the caller's wait. The entry may or may not have been stored.</param>
    /// <returns>A result with <see cref="WriteResult.Succeeded"/> true, or an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or over 1,024 characters.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ttl"/> is out of range.</exception>
    /// <exception cref="InvalidOperationException">The type's read strategy is not cache-aside.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    Task<WriteResult> StoreInCacheAsync(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a value using the type's write strategy. Write-through succeeds once the writer has finished.
    /// Write-behind succeeds once the value is in the cache and the database write is queued.
    /// Write-around succeeds once the writer has finished, and stores nothing in the cache.
    /// </summary>
    /// <param name="key">The caller's key without the prefix. Not empty, and at most 1,024 characters.</param>
    /// <param name="value">The value to write. Not null.</param>
    /// <param name="ttl">Greater than zero and at most the effective MaxTtl. Null uses the effective DefaultTtl.</param>
    /// <param name="cancellationToken">
    /// Passed to the writer. If the caller cancels after the database write has started, the write may still have
    /// completed, so the outcome is unknown. A queued write is not cancelled by this token.
    /// </param>
    /// <returns>A result with <see cref="WriteResult.Succeeded"/> true, or an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> or <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or over 1,024 characters.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ttl"/> is out of range.</exception>
    /// <exception cref="InvalidOperationException">The policy has no write strategy, or write-behind is used while its background service is not running.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    Task<WriteResult> WriteAsync(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an entry from the cache only, and cancels any queued write for the key. The database is not touched.
    /// </summary>
    /// <param name="key">The caller's key without the prefix. Not empty, and at most 1,024 characters.</param>
    /// <param name="cancellationToken">Stops the caller's wait. The entry may or may not have been removed.</param>
    /// <returns>A result with <see cref="WriteResult.Succeeded"/> true whether or not an entry existed, or an error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty or over 1,024 characters.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    Task<WriteResult> RemoveAsync(string key, CancellationToken cancellationToken = default);
}
