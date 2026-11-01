namespace CacheMe;

/// <summary>What a store returns for a key.</summary>
public sealed class CacheEntry
{
    /// <summary>The serialized value.</summary>
    public required ReadOnlyMemory<byte> Value { get; init; }

    /// <summary>When the entry was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>How long the entry can stay in the cache.</summary>
    public required TimeSpan Ttl { get; init; }

    /// <summary>Whether the key has a queued write.</summary>
    public required bool HasQueuedWrite { get; init; }
}
