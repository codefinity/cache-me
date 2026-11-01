namespace CacheMe;

/// <summary>Constants naming the metrics, for dashboards. All are tagged with value_type, except where noted.</summary>
public static class CacheMeMetrics
{
    /// <summary>The meter's name.</summary>
    public const string MeterName = "CacheMe";

    /// <summary>Counter of cache hits.</summary>
    public const string Hits = "cacheme.hits";

    /// <summary>Counter of cache misses.</summary>
    public const string Misses = "cacheme.misses";

    /// <summary>Counter of errors, also tagged with error_kind.</summary>
    public const string Errors = "cacheme.errors";

    /// <summary>Observable gauge of queued writes, with no value_type tag.</summary>
    public const string QueueDepth = "cacheme.write_behind.queue_depth";
}
