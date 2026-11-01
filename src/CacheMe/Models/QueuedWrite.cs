namespace CacheMe;

/// <summary>A claimed write-behind write.</summary>
public sealed class QueuedWrite
{
    /// <summary>The full key.</summary>
    public required string Key { get; init; }

    /// <summary>The policy's settings key.</summary>
    public required string ValueType { get; init; }

    /// <summary>The serialized value.</summary>
    public required ReadOnlyMemory<byte> Value { get; init; }

    /// <summary>Applied to the entry when the write completes.</summary>
    public required TimeSpan Ttl { get; init; }

    /// <summary>When the write was first queued.</summary>
    public required DateTimeOffset FirstQueuedAt { get; init; }

    /// <summary>How many tries have failed so far.</summary>
    public required int Attempts { get; init; }

    /// <summary>Changes when a newer value replaces this one.</summary>
    public required string Version { get; init; }
}
