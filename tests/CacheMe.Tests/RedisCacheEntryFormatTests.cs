using System.Text;

namespace CacheMe.Tests;

public sealed class RedisCacheEntryFormatTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact, Trait("Requirement", "FR-008"), Trait("Requirement", "FR-034")]
    public void DecodeEntry_EncodedEntry_RoundTrips()
    {
        byte[] value = [0, 255, 124, 10];

        var entry = RedisCacheEntryFormat.DecodeEntry(RedisCacheEntryFormat.EncodeEntry(value, Now, TimeSpan.FromMinutes(5)), hasQueuedWrite: true);

        Assert.NotNull(entry);
        Assert.Equal(value, entry.Value.ToArray());
        Assert.Equal(Now, entry.CreatedAt);
        Assert.Equal(TimeSpan.FromMinutes(5), entry.Ttl);
        Assert.True(entry.HasQueuedWrite);
    }

    [Fact, Trait("Requirement", "FR-016")]
    public void DecodeEntry_OtherData_ReturnsNull()
    {
        var entry = RedisCacheEntryFormat.DecodeEntry(Encoding.UTF8.GetBytes("not valid data, and long enough"), hasQueuedWrite: false);

        Assert.Null(entry);
    }

    [Fact, Trait("Requirement", "FR-016")]
    public void DecodeEntry_ShorterThanHeader_ReturnsNull()
    {
        var entry = RedisCacheEntryFormat.DecodeEntry([0xCA, 1], hasQueuedWrite: false);

        Assert.Null(entry);
    }

    [Fact, Trait("Requirement", "FR-023")]
    public void DecodeWrite_ValueTypeAndValueContainSeparator_RoundTrips()
    {
        byte[] value = [(byte)'|', 0, (byte)'|', 255];

        var write = RedisCacheEntryFormat.DecodeWrite("shop:a", RedisCacheEntryFormat.EncodeWrite("v1", 3, TimeSpan.FromSeconds(90), Now, "Odd|Type", value));

        Assert.Equal("shop:a", write.Key);
        Assert.Equal("v1", write.Version);
        Assert.Equal(3, write.Attempts);
        Assert.Equal(TimeSpan.FromSeconds(90), write.Ttl);
        Assert.Equal(Now, write.FirstQueuedAt);
        Assert.Equal("Odd|Type", write.ValueType);
        Assert.Equal(value, write.Value.ToArray());
    }

    [Fact, Trait("Requirement", "FR-034")]
    public void Milliseconds_LessThanOneMillisecond_ReturnsOne()
    {
        Assert.Equal(1, RedisCacheEntryFormat.Milliseconds(TimeSpan.FromTicks(1)));
    }
}
