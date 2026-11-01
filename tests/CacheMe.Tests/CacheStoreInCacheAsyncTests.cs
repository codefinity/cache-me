using CacheMe.Tests.Support;
using Moq;

namespace CacheMe.Tests;

public sealed class CacheStoreInCacheAsyncTests
{
    private readonly CacheHarness _h = new();

    [Fact, Trait("Requirement", "FR-037")]
    public async Task StoreInCacheAsync_NullKey_ThrowsArgumentNullException()
    {
        var cache = _h.Create(Policies.CacheAside());

        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.StoreInCacheAsync(null!, _h.Value, cancellationToken: _h.Token));
    }

    [Fact, Trait("Requirement", "FR-037")]
    public async Task StoreInCacheAsync_NullValue_ThrowsArgumentNullException()
    {
        var cache = _h.Create(Policies.CacheAside());

        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.StoreInCacheAsync(CacheHarness.Key, null!, cancellationToken: _h.Token));
    }

    [Fact, Trait("Requirement", "FR-037")]
    public async Task StoreInCacheAsync_EmptyKey_ThrowsArgumentException()
    {
        var cache = _h.Create(Policies.CacheAside());

        await Assert.ThrowsAsync<ArgumentException>(() => cache.StoreInCacheAsync("", _h.Value, cancellationToken: _h.Token));
    }

    [Fact, Trait("Requirement", "FR-037")]
    public async Task StoreInCacheAsync_KeyOver1024Characters_ThrowsArgumentException()
    {
        var cache = _h.Create(Policies.CacheAside());

        await Assert.ThrowsAsync<ArgumentException>(() => cache.StoreInCacheAsync(new string('k', 1025), _h.Value, cancellationToken: _h.Token));
    }

    [Theory, Trait("Requirement", "FR-037")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task StoreInCacheAsync_NonPositiveTtl_ThrowsArgumentOutOfRangeException(int seconds)
    {
        var cache = _h.Create(Policies.CacheAside());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cache.StoreInCacheAsync(CacheHarness.Key, _h.Value, TimeSpan.FromSeconds(seconds), _h.Token));
    }

    [Fact, Trait("Requirement", "NFR-002")]
    public async Task StoreInCacheAsync_TtlOverMaxTtl_ThrowsArgumentOutOfRangeException()
    {
        var cache = _h.Create(Policies.CacheAside());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cache.StoreInCacheAsync(CacheHarness.Key, _h.Value, TimeSpan.FromMinutes(6), _h.Token));
    }

    [Fact, Trait("Requirement", "FR-006")]
    public async Task StoreInCacheAsync_ReadStrategyIsNotCacheAside_ThrowsInvalidOperationException()
    {
        var cache = _h.Create(Policies.ReadThrough());

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.StoreInCacheAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token));
    }

    [Fact, Trait("Requirement", "FR-005"), Trait("Requirement", "FR-006")]
    public async Task StoreInCacheAsync_CacheAside_StoresValueWithPrefixedKey()
    {
        var cache = _h.Create(Policies.CacheAside());

        var result = await cache.StoreInCacheAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.True(result.Succeeded);
        _h.VerifyStored(TimeSpan.FromMinutes(5), Times.Once());
    }

    [Fact, Trait("Requirement", "FR-006")]
    public async Task StoreInCacheAsync_PolicyAlsoHasWriteThrough_DoesNotCallWriter()
    {
        var cache = _h.Create(Policies.CacheAside(WriteStrategy.WriteThrough));

        await cache.StoreInCacheAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        _h.Writer.Verify(w => w.WriteAsync(It.IsAny<string>(), It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact, Trait("Requirement", "FR-034")]
    public async Task StoreInCacheAsync_TtlGiven_UsesIt()
    {
        var cache = _h.Create(Policies.CacheAside());

        await cache.StoreInCacheAsync(CacheHarness.Key, _h.Value, TimeSpan.FromMinutes(1), _h.Token);

        _h.VerifyStored(TimeSpan.FromMinutes(1), Times.Once());
    }

    [Fact, Trait("Requirement", "NFR-002")]
    public async Task StoreInCacheAsync_TtlEqualsMaxTtl_IsAccepted()
    {
        var cache = _h.Create(Policies.CacheAside());

        await cache.StoreInCacheAsync(CacheHarness.Key, _h.Value, TimeSpan.FromMinutes(5), _h.Token);

        _h.VerifyStored(TimeSpan.FromMinutes(5), Times.Once());
    }

    [Fact, Trait("Requirement", "NFR-002")]
    public async Task StoreInCacheAsync_PolicyRaisesMaxTtl_AcceptsLongerTtl()
    {
        _h.Options.Policies["Product"] = new CachePolicyOptions { MaxTtl = TimeSpan.FromHours(1) };
        var cache = _h.Create(Policies.CacheAside());

        await cache.StoreInCacheAsync(CacheHarness.Key, _h.Value, TimeSpan.FromMinutes(30), _h.Token);

        _h.VerifyStored(TimeSpan.FromMinutes(30), Times.Once());
    }

    [Fact, Trait("Requirement", "FR-038")]
    public async Task StoreInCacheAsync_ValueOverMaxValueBytes_ReturnsValueTooLarge()
    {
        _h.Options.MaxValueBytes = 2;
        var cache = _h.Create(Policies.CacheAside());

        var result = await cache.StoreInCacheAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.False(result.Succeeded);
        Assert.Equal(CacheErrorKind.ValueTooLarge, result.Error!.Kind);
        _h.VerifyNotStored();
    }

    [Fact, Trait("Requirement", "FR-038")]
    public async Task StoreInCacheAsync_ValueExactlyAtMaxValueBytes_IsStored()
    {
        _h.Options.MaxValueBytes = CacheHarness.Bytes.Length;
        var cache = _h.Create(Policies.CacheAside());

        var result = await cache.StoreInCacheAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.True(result.Succeeded);
    }

    [Fact, Trait("Requirement", "TD-007")]
    public async Task StoreInCacheAsync_SerializerThrows_ReturnsSerializationFailed()
    {
        _h.Serializer.Setup(s => s.Serialize(It.IsAny<Product>())).Throws(new NotSupportedException());
        var cache = _h.Create(Policies.CacheAside());

        var result = await cache.StoreInCacheAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.Equal(CacheErrorKind.SerializationFailed, result.Error!.Kind);
    }

    [Fact, Trait("Requirement", "TD-007")]
    public async Task StoreInCacheAsync_StoreFails_ReturnsStoreUnavailable()
    {
        _h.Store.Setup(s => s.SetAsync(CacheHarness.FullKey, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis is down"));
        var cache = _h.Create(Policies.CacheAside());

        var result = await cache.StoreInCacheAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.Equal(CacheErrorKind.StoreUnavailable, result.Error!.Kind);
    }

    [Fact, Trait("Requirement", "TD-005")]
    public async Task StoreInCacheAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var cache = _h.Create(Policies.CacheAside());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.StoreInCacheAsync(CacheHarness.Key, _h.Value, cancellationToken: cancelled.Token));
    }

    [Fact, Trait("Requirement", "FR-008")]
    public async Task StoreInCacheAsync_AnyValue_PassesCurrentTimeAsCreatedAt()
    {
        var cache = _h.Create(Policies.CacheAside());

        await cache.StoreInCacheAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        _h.Store.Verify(s => s.SetAsync(CacheHarness.FullKey, It.IsAny<ReadOnlyMemory<byte>>(), _h.Clock.Now, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
