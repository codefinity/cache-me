using Microsoft.Extensions.Logging;

namespace CacheMe;

/// <summary>Runs refreshes off the request path, and cancels them when the application shuts down.</summary>
internal sealed class BackgroundWork : IBackgroundWork, IDisposable
{
    private readonly CancellationTokenSource _shutdown = new();
    private readonly CancellationToken _token;
    private readonly ILogger<BackgroundWork> _logger;

    public BackgroundWork(ILogger<BackgroundWork> logger)
    {
        _logger = logger;
        _token = _shutdown.Token;
    }

    public void Run(Func<CancellationToken, Task> work)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await work(_token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_token.IsCancellationRequested)
            {
                // The application is shutting down.
            }
            catch (Exception exception)
            {
                _logger.LogWarning("Background work failed: {ExceptionType}.", exception.GetType().Name);
            }
        }, CancellationToken.None);
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _shutdown.Dispose();
    }
}
