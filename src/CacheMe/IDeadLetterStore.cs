namespace CacheMe;

/// <summary>
/// Inject this to inspect and clear failed writes. It is registered only when the store supports
/// write-behind. The library does not replay these writes.
/// </summary>
public interface IDeadLetterStore
{
    /// <summary>Lists stored failed writes, oldest first.</summary>
    /// <param name="maxCount">The most writes to return. At least 1.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Up to <paramref name="maxCount"/> writes.</returns>
    Task<IReadOnlyList<DeadLetterWrite>> ListAsync(int maxCount, CancellationToken cancellationToken);

    /// <summary>Deletes a failed write once it has been dealt with.</summary>
    /// <param name="id">The write's <see cref="DeadLetterWrite.Id"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Whether it existed.</returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
}
