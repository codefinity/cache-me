namespace CacheMe;

/// <summary>
/// Replaces the default System.Text.Json serializer. The serializer is a singleton, so members must be
/// safe for concurrent use.
/// </summary>
public interface ICacheSerializer
{
    /// <summary>Turns a value into bytes. Any exception counts as a failed serialize.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value. Not null.</param>
    /// <returns>The bytes to store.</returns>
    byte[] Serialize<T>(T value) where T : class;

    /// <summary>Turns stored bytes back into a value. Any exception counts as unreadable data.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="data">The stored bytes.</param>
    /// <returns>The value.</returns>
    T Deserialize<T>(ReadOnlyMemory<byte> data) where T : class;
}
