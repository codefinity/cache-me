namespace CacheMe;

/// <summary>Runs refreshes off the request path, so tests can run them directly.</summary>
internal interface IBackgroundWork
{
    /// <summary>Starts the work in the background.</summary>
    /// <param name="work">The work to run.</param>
    void Run(Func<CancellationToken, Task> work);
}
