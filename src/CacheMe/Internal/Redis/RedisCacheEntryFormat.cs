using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace CacheMe;

/// <summary>The bytes stored in Redis for entries and for queued writes.</summary>
internal static class RedisCacheEntryFormat
{
    private const byte Marker = 0xCA;
    private const byte FormatVersion = 1;
    private const int HeaderLength = 18;
    private const byte Separator = (byte)'|';

    /// <summary>Encodes an entry: marker, format version, created time, TTL, then the value.</summary>
    public static byte[] EncodeEntry(ReadOnlySpan<byte> value, DateTimeOffset createdAt, TimeSpan ttl)
    {
        var bytes = new byte[HeaderLength + value.Length];
        bytes[0] = Marker;
        bytes[1] = FormatVersion;
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(2), createdAt.ToUnixTimeMilliseconds());
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(10), Milliseconds(ttl));
        value.CopyTo(bytes.AsSpan(HeaderLength));
        return bytes;
    }

    /// <summary>Decodes an entry. Returns null if the bytes are not an entry in this format.</summary>
    public static CacheEntry? DecodeEntry(byte[] bytes, bool hasQueuedWrite)
    {
        if (bytes.Length < HeaderLength || bytes[0] != Marker || bytes[1] != FormatVersion)
        {
            return null;
        }

        return new CacheEntry
        {
            CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(2))),
            Ttl = TimeSpan.FromMilliseconds(BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(10))),
            HasQueuedWrite = hasQueuedWrite,
            Value = bytes.AsMemory(HeaderLength)
        };
    }

    /// <summary>
    /// Encodes a queued write: version|attempts|ttl|first queued|type length|type, then the value.
    /// Lua scripts read only the first two fields.
    /// </summary>
    public static byte[] EncodeWrite(string version, int attempts, TimeSpan ttl, DateTimeOffset firstQueuedAt, string valueType, ReadOnlySpan<byte> value)
    {
        var type = Encoding.UTF8.GetBytes(valueType);
        var header = Encoding.ASCII.GetBytes(string.Create(
            CultureInfo.InvariantCulture,
            $"{version}|{attempts}|{Milliseconds(ttl)}|{firstQueuedAt.ToUnixTimeMilliseconds()}|{type.Length}|"));

        var bytes = new byte[header.Length + type.Length + value.Length];
        header.CopyTo(bytes, 0);
        type.CopyTo(bytes, header.Length);
        value.CopyTo(bytes.AsSpan(header.Length + type.Length));
        return bytes;
    }

    /// <summary>Decodes a queued write.</summary>
    public static QueuedWrite DecodeWrite(string key, byte[] bytes)
    {
        var position = 0;
        var version = NextField(bytes, ref position);
        var attempts = int.Parse(NextField(bytes, ref position), CultureInfo.InvariantCulture);
        var ttl = long.Parse(NextField(bytes, ref position), CultureInfo.InvariantCulture);
        var firstQueued = long.Parse(NextField(bytes, ref position), CultureInfo.InvariantCulture);
        var typeLength = int.Parse(NextField(bytes, ref position), CultureInfo.InvariantCulture);

        return new QueuedWrite
        {
            Key = key,
            Version = version,
            Attempts = attempts,
            Ttl = TimeSpan.FromMilliseconds(ttl),
            FirstQueuedAt = DateTimeOffset.FromUnixTimeMilliseconds(firstQueued),
            ValueType = Encoding.UTF8.GetString(bytes, position, typeLength),
            Value = bytes.AsMemory(position + typeLength)
        };
    }

    /// <summary>A TTL in whole milliseconds, never below one.</summary>
    public static long Milliseconds(TimeSpan ttl) => Math.Max(1, (long)Math.Ceiling(ttl.TotalMilliseconds));

    private static string NextField(byte[] bytes, ref int position)
    {
        var end = Array.IndexOf(bytes, Separator, position);
        var field = Encoding.ASCII.GetString(bytes, position, end - position);
        position = end + 1;
        return field;
    }
}
