namespace CacheMe;

/// <summary>The outcome of a read.</summary>
public enum ReadStatus
{
    /// <summary>A value was found.</summary>
    Found,

    /// <summary>No value exists to return.</summary>
    NotFound,

    /// <summary>The read failed. See <see cref="ReadResult{T}.Error"/>.</summary>
    Failed
}
