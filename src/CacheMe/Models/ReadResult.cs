namespace CacheMe;

/// <summary>The result of a read.</summary>
/// <typeparam name="T">The value type.</typeparam>
public sealed class ReadResult<T> where T : class
{
    private ReadResult(ReadStatus status, T? value, CacheError? error)
    {
        Status = status;
        Value = value;
        Error = error;
    }

    /// <summary>The outcome.</summary>
    public ReadStatus Status { get; }

    /// <summary>The value. Set only when <see cref="Status"/> is <see cref="ReadStatus.Found"/>.</summary>
    public T? Value { get; }

    /// <summary>The error. Set only when <see cref="Status"/> is <see cref="ReadStatus.Failed"/>.</summary>
    public CacheError? Error { get; }

    /// <summary>Creates a result for a value that was found.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The result.</returns>
    public static ReadResult<T> Found(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ReadResult<T>(ReadStatus.Found, value, null);
    }

    /// <summary>Creates a result for no value.</summary>
    /// <returns>The result.</returns>
    public static ReadResult<T> NotFound() => new(ReadStatus.NotFound, null, null);

    /// <summary>Creates a result for a failed read.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The result.</returns>
    public static ReadResult<T> Failed(CacheError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ReadResult<T>(ReadStatus.Failed, null, error);
    }
}
