namespace CacheMe;

/// <summary>
/// Application code that saves one value to the database. A new instance is resolved in a new
/// DI scope for each use, and write-behind uses one scope and one call per queued write.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
public interface ICacheWriter<T> where T : class
{
    /// <summary>
    /// Saves one value to the database. Runs on the caller's path, or in the background for write-behind, and always
    /// outside the caller's database transaction. Any exception counts as a failed write: write-through and write-around
    /// return <see cref="CacheErrorKind.WriterFailed"/>, and write-behind retries.
    /// </summary>
    /// <param name="key">The caller's key without the prefix.</param>
    /// <param name="value">The value to save.</param>
    /// <param name="cancellationToken">The caller's token, or a background token for write-behind.</param>
    /// <returns>A task that completes when the value is saved.</returns>
    Task WriteAsync(string key, T value, CancellationToken cancellationToken);
}
