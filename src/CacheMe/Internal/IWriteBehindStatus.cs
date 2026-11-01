namespace CacheMe;

/// <summary>Tells whether the write-behind background service is running.</summary>
internal interface IWriteBehindStatus
{
    /// <summary>Whether the background service is running.</summary>
    bool IsRunning { get; }
}
