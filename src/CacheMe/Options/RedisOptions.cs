namespace CacheMe;

/// <summary>Redis settings.</summary>
public sealed class RedisOptions
{
    /// <summary>Connection the library creates. Required when UseRedis has no connection supplied. One endpoint only.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>Time limit for each Redis operation.</summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(1);
}
