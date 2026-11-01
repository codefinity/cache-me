namespace CacheMe;

/// <summary>The settings under the CacheMe configuration section.</summary>
public sealed class CacheMeOptions
{
    /// <summary>Added as is before every key. Change it (for example, shop:v2:) to version keys after a type changes.</summary>
    public string KeyPrefix { get; set; } = "";

    /// <summary>TTL for an entry when a write gives none. Required.</summary>
    public TimeSpan? DefaultTtl { get; set; }

    /// <summary>Longest TTL allowed, which limits staleness from outside database changes.</summary>
    public TimeSpan MaxTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Share of TTL after which a read starts a background reload.</summary>
    public double RefreshThreshold { get; set; } = 0.75;

    /// <summary>Largest serialized value a write accepts.</summary>
    public int MaxValueBytes { get; set; } = 1_048_576;

    /// <summary>Redis settings.</summary>
    public RedisOptions Redis { get; set; } = new();

    /// <summary>Write-behind settings, which apply to every type that uses write-behind.</summary>
    public WriteBehindOptions WriteBehind { get; set; } = new();

    /// <summary>Per-type overrides, by settings key.</summary>
    public Dictionary<string, CachePolicyOptions> Policies { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
