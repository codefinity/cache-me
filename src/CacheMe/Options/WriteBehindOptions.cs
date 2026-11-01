namespace CacheMe;

/// <summary>Write-behind settings, which apply to every type that uses write-behind.</summary>
public sealed class WriteBehindOptions
{
    /// <summary>Longest a write waits before a flush.</summary>
    public TimeSpan MaxFlushDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Most writes in one flush.</summary>
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>Most queued writes in total, across all instances. New writes return an error at this limit.</summary>
    public int MaxQueueLength { get; set; } = 100_000;

    /// <summary>Longest a flush runs at shutdown. Keep it below the host's shutdown timeout.</summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Retry settings for a queued write.</summary>
    public WriteBehindRetryOptions Retry { get; set; } = new();

    /// <summary>Dead-letter settings.</summary>
    public DeadLetterOptions DeadLetter { get; set; } = new();
}
