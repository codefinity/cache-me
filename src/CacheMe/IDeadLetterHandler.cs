namespace CacheMe;

/// <summary>
/// Optional application code that is notified after a queued write moves to the dead-letter store.
/// A new instance is resolved in a new DI scope for each use.
/// </summary>
public interface IDeadLetterHandler
{
    /// <summary>
    /// Handles a failed write. Runs in the background, after the write is stored. Any exception is logged, and the
    /// write stays in the dead-letter store until its retention ends.
    /// </summary>
    /// <param name="write">The failed write, including its raw payload, which may not be readable after a type change.</param>
    /// <param name="cancellationToken">Cancelled at shutdown.</param>
    /// <returns>A task that completes when handling is finished.</returns>
    Task HandleAsync(DeadLetterWrite write, CancellationToken cancellationToken);
}
