using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CacheMe;

/// <summary>Runs the write-behind processor on a loop, and flushes at shutdown.</summary>
internal sealed class WriteBehindService : BackgroundService, IWriteBehindStatus
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    private readonly WriteBehindProcessor _processor;
    private readonly ILogger<WriteBehindService> _logger;
    private volatile bool _isRunning;

    public WriteBehindService(WriteBehindProcessor processor, ILogger<WriteBehindService> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    /// <summary>Used by caches when no policy uses write-behind.</summary>
    internal static IWriteBehindStatus NotRunning { get; } = new StoppedStatus();

    public bool IsRunning => _isRunning;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _isRunning = true;
        return base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _isRunning = false;
        await base.StopAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await _processor.FlushAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning("The shutdown flush failed ({ExceptionType}), so writes stay queued.", exception.GetType().Name);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                processed = await _processor.FlushOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning("A write-behind flush failed ({ExceptionType}) and will be tried again.", exception.GetType().Name);
            }

            if (processed == 0)
            {
                try
                {
                    await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private sealed class StoppedStatus : IWriteBehindStatus
    {
        public bool IsRunning => false;
    }
}
