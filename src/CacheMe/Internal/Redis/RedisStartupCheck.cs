using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CacheMe;

/// <summary>
/// Logs warnings at startup if Redis cannot be reached, or if its settings are unsafe for the
/// write-behind queue. It never stops the application.
/// </summary>
internal sealed class RedisStartupCheck(RedisCacheStore store, CachePolicyRegistry policies, ILogger<RedisStartupCheck> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await store.PingAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Redis could not be reached at startup ({ExceptionType}). Reads fall back to loaders where they can.", exception.GetType().Name);
            return;
        }

        if (policies.UsesWriteBehind)
        {
            await CheckQueueSafetyAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task CheckQueueSafetyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var memory = await store.ReadInfoAsync("memory", cancellationToken).ConfigureAwait(false);
            if (memory.TryGetValue("maxmemory_policy", out var eviction) && eviction.StartsWith("allkeys-", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Redis uses the {EvictionPolicy} eviction policy, which can remove queued write-behind writes.", eviction);
            }

            var persistence = await store.ReadInfoAsync("persistence", cancellationToken).ConfigureAwait(false);
            if (persistence.TryGetValue("aof_enabled", out var aof) && aof == "0")
            {
                logger.LogWarning("Redis AOF persistence is off, so a Redis restart can lose queued write-behind writes.");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogDebug(exception, "Redis settings could not be read, so the write-behind safety check was skipped.");
        }
    }
}
