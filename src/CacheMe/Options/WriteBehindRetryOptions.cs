namespace CacheMe;

/// <summary>Retry settings for a queued write.</summary>
public sealed class WriteBehindRetryOptions
{
    /// <summary>Total tries for a queued write, including the first.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Wait before the second try. The wait then doubles each time.</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Longest wait between tries.</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
}
