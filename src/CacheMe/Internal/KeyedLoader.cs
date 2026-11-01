namespace CacheMe;

/// <summary>
/// Runs one load per key, per process. Callers share the load, and a caller that cancels only stops
/// waiting. The load is cancelled when no callers are left.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
internal sealed class KeyedLoader<T> where T : class
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Flight> _flights = new(StringComparer.Ordinal);

    public async Task<ReadResult<T>> RunAsync(string key, Func<CancellationToken, Task<ReadResult<T>>> load, CancellationToken cancellationToken)
    {
        Flight? flight;
        var starts = false;
        lock (_gate)
        {
            if (!_flights.TryGetValue(key, out flight))
            {
                flight = new Flight();
                _flights[key] = flight;
                starts = true;
            }

            flight.Waiters++;
        }

        if (starts)
        {
            _ = ExecuteAsync(key, flight, load);
        }

        try
        {
            return await flight.Completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                flight.Waiters--;
                if (flight.Waiters == 0 && !flight.Finished)
                {
                    flight.Cancellation.Cancel();
                }
            }
        }
    }

    private async Task ExecuteAsync(string key, Flight flight, Func<CancellationToken, Task<ReadResult<T>>> load)
    {
        ReadResult<T>? result = null;
        Exception? failure = null;
        try
        {
            result = await load(flight.Cancellation.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        lock (_gate)
        {
            flight.Finished = true;
            _flights.Remove(key);
            flight.Cancellation.Dispose();
        }

        if (failure is not null)
        {
            flight.Completion.SetException(failure);
        }
        else
        {
            flight.Completion.SetResult(result!);
        }
    }

    private sealed class Flight
    {
        public TaskCompletionSource<ReadResult<T>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationTokenSource Cancellation { get; } = new();

        public int Waiters { get; set; }

        public bool Finished { get; set; }
    }
}
