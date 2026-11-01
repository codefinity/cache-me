namespace CacheMe;

/// <summary>Settings for one value type, which override the general settings.</summary>
public sealed class CachePolicyOptions
{
    /// <summary>Default TTL for this type.</summary>
    public TimeSpan? DefaultTtl { get; set; }

    /// <summary>Staleness limit for this type.</summary>
    public TimeSpan? MaxTtl { get; set; }

    /// <summary>Refresh threshold for this type. Only for a type using refresh-ahead.</summary>
    public double? RefreshThreshold { get; set; }
}
