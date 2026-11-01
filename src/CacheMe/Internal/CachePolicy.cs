namespace CacheMe;

/// <summary>The read strategies a policy can use.</summary>
internal enum ReadStrategy
{
    /// <summary>The policy has no read strategy.</summary>
    None,

    /// <summary>The repository handles the miss.</summary>
    CacheAside,

    /// <summary>The library calls the loader on a miss.</summary>
    ReadThrough
}

/// <summary>The write strategies a policy can use.</summary>
internal enum WriteStrategy
{
    /// <summary>The policy has no write strategy.</summary>
    None,

    /// <summary>Database, then cache.</summary>
    WriteThrough,

    /// <summary>Cache and queue, database later.</summary>
    WriteBehind,

    /// <summary>Database, then remove the entry.</summary>
    WriteAround
}

/// <summary>The policy's strategies, loader and writer types, and settings key.</summary>
/// <typeparam name="T">The value type.</typeparam>
internal sealed class CachePolicy<T> where T : class
{
    /// <summary>The key under CacheMe:Policies, and the value type name in queued writes and metrics.</summary>
    public required string SettingsKey { get; init; }

    /// <summary>The read strategy.</summary>
    public ReadStrategy ReadStrategy { get; init; }

    /// <summary>Whether the refresh-ahead add-on is on.</summary>
    public bool RefreshAhead { get; init; }

    /// <summary>The write strategy.</summary>
    public WriteStrategy WriteStrategy { get; init; }

    /// <summary>The service type to resolve for the loader, if any.</summary>
    public Type? LoaderType { get; init; }

    /// <summary>The service type to resolve for the writer, if any.</summary>
    public Type? WriterType { get; init; }
}
