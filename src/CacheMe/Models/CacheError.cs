namespace CacheMe;

/// <summary>An error returned in a result.</summary>
public sealed class CacheError
{
    /// <summary>Creates an error.</summary>
    /// <param name="kind">What went wrong.</param>
    /// <param name="message">A description.</param>
    /// <param name="exception">The exception behind the error, if any.</param>
    public CacheError(CacheErrorKind kind, string message, Exception? exception = null)
    {
        Kind = kind;
        Message = message;
        Exception = exception;
    }

    /// <summary>What went wrong.</summary>
    public CacheErrorKind Kind { get; }

    /// <summary>A description.</summary>
    public string Message { get; }

    /// <summary>The exception behind the error, if any.</summary>
    public Exception? Exception { get; }
}
