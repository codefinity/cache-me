namespace CacheMe;

/// <summary>The result of a write, a cache-only store or a remove.</summary>
public sealed class WriteResult
{
    private WriteResult(bool succeeded, CacheError? error)
    {
        Succeeded = succeeded;
        Error = error;
    }

    /// <summary>Whether the operation succeeded. What this means for a write depends on the strategy.</summary>
    public bool Succeeded { get; }

    /// <summary>The error. Set only when <see cref="Succeeded"/> is false.</summary>
    public CacheError? Error { get; }

    /// <summary>Creates a result for a successful operation.</summary>
    /// <returns>The result.</returns>
    public static WriteResult Success() => new(true, null);

    /// <summary>Creates a result for a failed operation.</summary>
    /// <param name="error">The error.</param>
    /// <returns>The result.</returns>
    public static WriteResult Failed(CacheError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new WriteResult(false, error);
    }
}
