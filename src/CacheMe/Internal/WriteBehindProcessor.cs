using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CacheMe;

/// <summary>Processes the write-behind queue. Internal, but the tests call it directly.</summary>
internal sealed class WriteBehindProcessor
{
    private const int MaxConcurrentWrites = 8;

    private static readonly TimeSpan ClaimDuration = TimeSpan.FromMinutes(5);

    private readonly IWriteBehindStore _store;
    private readonly CachePolicyRegistry _policies;
    private readonly ICacheSerializer _serializer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICacheMetrics _metrics;
    private readonly IOptions<CacheMeOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WriteBehindProcessor> _logger;

    internal WriteBehindProcessor(
        IWriteBehindStore store,
        CachePolicyRegistry policies,
        ICacheSerializer serializer,
        IServiceScopeFactory scopeFactory,
        ICacheMetrics metrics,
        IOptions<CacheMeOptions> options,
        TimeProvider timeProvider,
        ILogger<WriteBehindProcessor> logger)
    {
        _store = store;
        _policies = policies;
        _serializer = serializer;
        _scopeFactory = scopeFactory;
        _metrics = metrics;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Claims and writes one batch.</summary>
    /// <param name="cancellationToken">Cancels the flush.</param>
    /// <returns>How many writes were processed.</returns>
    internal async Task<int> FlushOnceAsync(CancellationToken cancellationToken)
    {
        var settings = _options.Value.WriteBehind;
        var length = await _store.GetQueueLengthAsync(cancellationToken).ConfigureAwait(false);
        _metrics.RecordQueueDepth(length);

        var now = _timeProvider.GetUtcNow();
        var queuedBefore = length >= settings.MaxBatchSize ? now : now - settings.MaxFlushDelay;
        return await ClaimAndWriteAsync(queuedBefore, now, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Flushes until the queue is empty or the shutdown timeout passes.</summary>
    /// <param name="cancellationToken">Cancels the flush.</param>
    /// <returns>A task that completes when the flush ends.</returns>
    internal async Task FlushAllAsync(CancellationToken cancellationToken)
    {
        var timeout = _options.Value.WriteBehind.ShutdownTimeout;
        var deadline = _timeProvider.GetUtcNow() + timeout;

        using var timer = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timer.Token, cancellationToken);
        try
        {
            while (true)
            {
                var now = _timeProvider.GetUtcNow();
                var count = await ClaimAndWriteAsync(now, now, linked.Token).ConfigureAwait(false);
                if (count == 0 || _timeProvider.GetUtcNow() >= deadline)
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (timer.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("The shutdown flush stopped after {ShutdownTimeout}, with writes still queued.", timeout);
        }
    }

    private async Task<int> ClaimAndWriteAsync(DateTimeOffset queuedBefore, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var writes = await _store
            .ClaimAsync(_options.Value.WriteBehind.MaxBatchSize, queuedBefore, now, ClaimDuration, cancellationToken)
            .ConfigureAwait(false);

        var parallel = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxConcurrentWrites,
            CancellationToken = cancellationToken
        };
        await Parallel.ForEachAsync(writes, parallel, ProcessAsync).ConfigureAwait(false);
        return writes.Count;
    }

    private async ValueTask ProcessAsync(QueuedWrite write, CancellationToken cancellationToken)
    {
        try
        {
            if (!_policies.TryGet(write.ValueType, out var policy))
            {
                await DeadLetterAsync(write, $"No policy is registered for {write.ValueType}.", CacheErrorKind.WriterFailed, cancellationToken).ConfigureAwait(false);
                return;
            }

            object value;
            try
            {
                value = policy!.Deserialize(_serializer, write.Value);
            }
            catch (Exception exception)
            {
                await DeadLetterAsync(write, Describe(exception), CacheErrorKind.SerializationFailed, cancellationToken).ConfigureAwait(false);
                return;
            }

            try
            {
                var scope = _scopeFactory.CreateAsyncScope();
                await using (scope.ConfigureAwait(false))
                {
                    await policy.WriteAsync(scope.ServiceProvider, KeyWithoutPrefix(write.Key), value, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                await HandleWriterFailureAsync(write, exception, cancellationToken).ConfigureAwait(false);
                return;
            }

            await _store.CompleteAsync(write, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            // The store failed, so the claim expires and the write is tried again later.
            _logger.LogWarning("{ValueType}: the queue could not be updated ({ExceptionType}).", write.ValueType, exception.GetType().Name);
        }
    }

    private async Task HandleWriterFailureAsync(QueuedWrite write, Exception exception, CancellationToken cancellationToken)
    {
        var retry = _options.Value.WriteBehind.Retry;
        if (write.Attempts + 1 >= retry.MaxAttempts)
        {
            await DeadLetterAsync(write, Describe(exception), CacheErrorKind.WriterFailed, cancellationToken).ConfigureAwait(false);
            return;
        }

        _metrics.RecordError(write.ValueType, CacheErrorKind.WriterFailed);
        _logger.LogWarning("{ValueType}: a queued write failed and will be tried again ({ExceptionType}).", write.ValueType, exception.GetType().Name);

        var delay = retry.InitialDelay.TotalMilliseconds * Math.Pow(2, write.Attempts);
        var capped = TimeSpan.FromMilliseconds(Math.Min(delay, retry.MaxDelay.TotalMilliseconds));
        await _store.RetryLaterAsync(write, _timeProvider.GetUtcNow() + capped, cancellationToken).ConfigureAwait(false);
    }

    private async Task DeadLetterAsync(QueuedWrite write, string reason, CacheErrorKind kind, CancellationToken cancellationToken)
    {
        var failedAt = _timeProvider.GetUtcNow();
        _metrics.RecordError(write.ValueType, kind);
        await _store
            .MoveToDeadLetterAsync(write, reason, failedAt, _options.Value.WriteBehind.DeadLetter.Retention, cancellationToken)
            .ConfigureAwait(false);
        _logger.LogError("{ValueType}: a queued write moved to the dead-letter store with ID {Id}.", write.ValueType, write.Version);

        var deadLetter = new DeadLetterWrite
        {
            Id = write.Version,
            Key = write.Key,
            ValueType = write.ValueType,
            Value = write.Value,
            Attempts = write.Attempts + 1,
            FirstQueuedAt = write.FirstQueuedAt,
            FailedAt = failedAt,
            FailureReason = reason
        };

        try
        {
            var scope = _scopeFactory.CreateAsyncScope();
            await using (scope.ConfigureAwait(false))
            {
                var handler = scope.ServiceProvider.GetService<IDeadLetterHandler>();
                if (handler is not null)
                {
                    await handler.HandleAsync(deadLetter, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            _logger.LogError("{ValueType}: the dead-letter handler failed ({ExceptionType}).", write.ValueType, exception.GetType().Name);
        }
    }

    private string KeyWithoutPrefix(string fullKey)
    {
        var prefix = _options.Value.KeyPrefix;
        return fullKey.StartsWith(prefix, StringComparison.Ordinal) ? fullKey[prefix.Length..] : fullKey;
    }

    private static string Describe(Exception exception) => $"{exception.GetType().Name}: {exception.Message}";
}
