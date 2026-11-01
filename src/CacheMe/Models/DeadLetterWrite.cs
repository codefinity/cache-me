namespace CacheMe;

/// <summary>A write in the dead-letter store.</summary>
public sealed class DeadLetterWrite
{
    /// <summary>Identifies the write in the dead-letter store.</summary>
    public required string Id { get; init; }

    /// <summary>The full key.</summary>
    public required string Key { get; init; }

    /// <summary>The policy's settings key.</summary>
    public required string ValueType { get; init; }

    /// <summary>The raw payload, which may not be readable after a type change.</summary>
    public required ReadOnlyMemory<byte> Value { get; init; }

    /// <summary>How many tries were made.</summary>
    public required int Attempts { get; init; }

    /// <summary>When the write was first queued.</summary>
    public required DateTimeOffset FirstQueuedAt { get; init; }

    /// <summary>When the write failed for the last time.</summary>
    public required DateTimeOffset FailedAt { get; init; }

    /// <summary>Why the write failed.</summary>
    public required string FailureReason { get; init; }
}
