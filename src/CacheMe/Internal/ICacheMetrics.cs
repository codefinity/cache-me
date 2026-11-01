namespace CacheMe;

/// <summary>Records metrics, so tests can check them.</summary>
internal interface ICacheMetrics
{
    /// <summary>Records a cache hit.</summary>
    /// <param name="valueType">The policy's settings key.</param>
    void RecordHit(string valueType);

    /// <summary>Records a cache miss.</summary>
    /// <param name="valueType">The policy's settings key.</param>
    void RecordMiss(string valueType);

    /// <summary>Records an error.</summary>
    /// <param name="valueType">The policy's settings key.</param>
    /// <param name="kind">What went wrong.</param>
    void RecordError(string valueType, CacheErrorKind kind);

    /// <summary>Records the latest length of the write-behind queue.</summary>
    /// <param name="length">The number of queued writes.</param>
    void RecordQueueDepth(long length);
}
