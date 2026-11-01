using System.Text.Json;

namespace CacheMe;

/// <summary>The default serializer, using System.Text.Json with web defaults.</summary>
internal sealed class JsonCacheSerializer : ICacheSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public byte[] Serialize<T>(T value) where T : class => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    public T Deserialize<T>(ReadOnlyMemory<byte> data) where T : class =>
        JsonSerializer.Deserialize<T>(data.Span, Options)
        ?? throw new JsonException("The stored value is null.");
}
