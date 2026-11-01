namespace CacheMe;

/// <summary>What went wrong.</summary>
public enum CacheErrorKind
{
    /// <summary>The store failed or timed out.</summary>
    StoreUnavailable,

    /// <summary>The loader threw.</summary>
    LoaderFailed,

    /// <summary>The writer threw.</summary>
    WriterFailed,

    /// <summary>The write-behind queue is at MaxQueueLength.</summary>
    QueueFull,

    /// <summary>The serialized value is over MaxValueBytes.</summary>
    ValueTooLarge,

    /// <summary>The serializer threw on a write.</summary>
    SerializationFailed
}
