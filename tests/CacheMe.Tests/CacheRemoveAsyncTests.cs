using CacheMe.Tests.Support;
using Moq;

namespace CacheMe.Tests;

public sealed class CacheRemoveAsyncTests
{
    private readonly CacheHarness _h = new();

    [Fact, Trait("Requirement", "FR-037")]
    public async Task RemoveAsync_NullKey_ThrowsArgumentNullException()
    {
        var cache = _h.Create(Policies.CacheAside());

        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.RemoveAsync(null!, _h.Token));
    }

    [Fact, Trait("Requirement", "FR-037")]
    public async Task RemoveAsync_EmptyKey_ThrowsArgumentException()
    {
        var cache = _h.Create(Policies.CacheAside());

        await Assert.ThrowsAsync<ArgumentException>(() => cache.RemoveAsync("", _h.Token));
    }

    [Fact, Trait("Requirement", "FR-037")]
    public async Task RemoveAsync_KeyOver1024Characters_ThrowsArgumentException()
    {
        var cache = _h.Create(Policies.CacheAside());

        await Assert.ThrowsAsync<ArgumentException>(() => cache.RemoveAsync(new string('k', 1025), _h.Token));
    }

    [Fact, Trait("Requirement", "TD-005")]
    public async Task RemoveAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var cache = _h.Create(Policies.CacheAside());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.RemoveAsync(CacheHarness.Key, cancelled.Token));
    }

    [Fact, Trait("Requirement", "FR-032")]
    public async Task RemoveAsync_EntryExists_RemovesWithPrefixedKey()
    {
        _h.Store.Setup(s => s.RemoveAsync(CacheHarness.FullKey, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var cache = _h.Create(Policies.CacheAside());

        var result = await cache.RemoveAsync(CacheHarness.Key, _h.Token);

        Assert.True(result.Succeeded);
        _h.Store.Verify(s => s.RemoveAsync(CacheHarness.FullKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-032")]
    public async Task RemoveAsync_NoEntry_StillSucceeds()
    {
        _h.Store.Setup(s => s.RemoveAsync(CacheHarness.FullKey, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var cache = _h.Create(Policies.CacheAside());

        var result = await cache.RemoveAsync(CacheHarness.Key, _h.Token);

        Assert.True(result.Succeeded);
    }

    [Fact, Trait("Requirement", "FR-032")]
    public async Task RemoveAsync_StoreSupportsWriteBehind_CancelsQueuedWrite()
    {
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteBehind));

        await cache.RemoveAsync(CacheHarness.Key, _h.Token);

        _h.Store.Verify(s => s.CancelAsync(CacheHarness.FullKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-032")]
    public async Task RemoveAsync_AnyPolicy_DoesNotTouchDatabase()
    {
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteThrough));

        await cache.RemoveAsync(CacheHarness.Key, _h.Token);

        _h.Writer.Verify(w => w.WriteAsync(It.IsAny<string>(), It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact, Trait("Requirement", "TD-007")]
    public async Task RemoveAsync_StoreFails_ReturnsStoreUnavailable()
    {
        _h.Store.Setup(s => s.RemoveAsync(CacheHarness.FullKey, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Redis is down"));
        var cache = _h.Create(Policies.CacheAside());

        var result = await cache.RemoveAsync(CacheHarness.Key, _h.Token);

        Assert.False(result.Succeeded);
        Assert.Equal(CacheErrorKind.StoreUnavailable, result.Error!.Kind);
    }
}
