namespace CacheMe;

/// <summary>Dead-letter settings.</summary>
public sealed class DeadLetterOptions
{
    /// <summary>How long failed writes stay in the dead-letter store.</summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);
}
