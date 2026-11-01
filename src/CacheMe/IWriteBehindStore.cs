namespace CacheMe;

/// <summary>
/// Also implemented by a store that supports write-behind. These operations must be atomic,
/// because many instances share the queue. <see cref="ICacheStore.SetAsync"/> must not overwrite
/// a value that has a queued write, and <see cref="DeadLetterWrite.Id"/> must equal
/// <see cref="QueuedWrite.Version"/>.
/// </summary>
public interface IWriteBehindStore : ICacheStore, IDeadLetterStore
{
    /// <summary>
    /// In one step, stores the value with no expiry, replaces any queued value for the key, and queues the write.
    /// </summary>
    /// <param name="key">The full key.</param>
    /// <param name="valueType">The policy's settings key.</param>
    /// <param name="value">The serialized value.</param>
    /// <param name="queuedAt">When the write was queued.</param>
    /// <param name="ttl">Applied to the entry when the write completes.</param>
    /// <param name="maxQueueLength">The queue limit.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>False if the queue is full and the key is not already queued.</returns>
    Task<bool> TryEnqueueAsync(string key, string valueType, ReadOnlyMemory<byte> value, DateTimeOffset queuedAt, TimeSpan ttl, int maxQueueLength, CancellationToken cancellationToken);

    /// <summary>
    /// Claims up to <paramref name="maxCount"/> due writes for one instance. A key has only one queued write, and it is
    /// claimed by one instance at a time. An unfinished claim becomes available again after
    /// <paramref name="claimDuration"/>, so a crash loses nothing.
    /// </summary>
    /// <param name="maxCount">The most writes to claim.</param>
    /// <param name="queuedBefore">The oldest age to claim: only writes queued before this time are due.</param>
    /// <param name="now">The current time, to check retry times.</param>
    /// <param name="claimDuration">How long the claim lasts.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The claimed writes.</returns>
    Task<IReadOnlyList<QueuedWrite>> ClaimAsync(int maxCount, DateTimeOffset queuedBefore, DateTimeOffset now, TimeSpan claimDuration, CancellationToken cancellationToken);

    /// <summary>Removes a finished write and applies its TTL to the entry, unless a newer write has replaced it.</summary>
    /// <param name="write">The claimed write.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the write is removed.</returns>
    Task CompleteAsync(QueuedWrite write, CancellationToken cancellationToken);

    /// <summary>Releases a failed write until <paramref name="nextAttemptAt"/> and adds one to its attempts.</summary>
    /// <param name="write">The claimed write.</param>
    /// <param name="nextAttemptAt">The next try.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the write is released.</returns>
    Task RetryLaterAsync(QueuedWrite write, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken);

    /// <summary>Moves a write to the dead-letter store for <paramref name="retention"/> and applies its TTL to the entry.</summary>
    /// <param name="write">The claimed write.</param>
    /// <param name="failureReason">For the handler and operators.</param>
    /// <param name="failedAt">When the write failed for the last time.</param>
    /// <param name="retention">How long the failed write stays in the dead-letter store.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the write is moved.</returns>
    Task MoveToDeadLetterAsync(QueuedWrite write, string failureReason, DateTimeOffset failedAt, TimeSpan retention, CancellationToken cancellationToken);

    /// <summary>Removes the queued write for a key.</summary>
    /// <param name="key">The full key.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Whether one was queued.</returns>
    Task<bool> CancelAsync(string key, CancellationToken cancellationToken);

    /// <summary>Counts queued writes.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The count.</returns>
    Task<long> GetQueueLengthAsync(CancellationToken cancellationToken);
}
