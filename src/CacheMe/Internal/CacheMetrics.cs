using System.Diagnostics.Metrics;

namespace CacheMe;

/// <summary>Publishes the metrics named in <see cref="CacheMeMetrics"/>.</summary>
internal sealed class CacheMetrics : ICacheMetrics, IDisposable
{
    private const string ValueTypeTag = "value_type";
    private const string ErrorKindTag = "error_kind";

    private readonly Meter _meter = new(CacheMeMetrics.MeterName);
    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _errors;
    private long _queueDepth;

    public CacheMetrics()
    {
        _hits = _meter.CreateCounter<long>(CacheMeMetrics.Hits);
        _misses = _meter.CreateCounter<long>(CacheMeMetrics.Misses);
        _errors = _meter.CreateCounter<long>(CacheMeMetrics.Errors);
        _meter.CreateObservableGauge(CacheMeMetrics.QueueDepth, () => Interlocked.Read(ref _queueDepth));
    }

    public void RecordHit(string valueType) => _hits.Add(1, new KeyValuePair<string, object?>(ValueTypeTag, valueType));

    public void RecordMiss(string valueType) => _misses.Add(1, new KeyValuePair<string, object?>(ValueTypeTag, valueType));

    public void RecordError(string valueType, CacheErrorKind kind) =>
        _errors.Add(1, new KeyValuePair<string, object?>(ValueTypeTag, valueType), new KeyValuePair<string, object?>(ErrorKindTag, kind.ToString()));

    public void RecordQueueDepth(long length) => Interlocked.Exchange(ref _queueDepth, length);

    public void Dispose() => _meter.Dispose();
}
