using CacheMe.Tests.Support;
using Microsoft.Extensions.Logging;
using Moq;

namespace CacheMe.Tests;

public sealed class CacheGetAsyncTests
{
    private readonly CacheHarness _h = new();

    [Fact, Trait("Requirement", "FR-037")]
    public async Task GetAsync_NullKey_ThrowsArgumentNullException()
    {
        var cache = _h.Create(Policies.CacheAside());

        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.GetAsync(null!, _h.Token));
    }

    [Fact, Trait("Requirement", "FR-037")]
    public async Task GetAsync_EmptyKey_ThrowsArgumentException()
    {
        var cache = _h.Create(Policies.CacheAside());

        await Assert.ThrowsAsync<ArgumentException>(() => cache.GetAsync("", _h.Token));
    }

    [Fact, Trait("Requirement", "FR-037")]
    public async Task GetAsync_KeyOver1024Characters_ThrowsArgumentException()
    {
        var cache = _h.Create(Policies.CacheAside());

        await Assert.ThrowsAsync<ArgumentException>(() => cache.GetAsync(new string('k', 1025), _h.Token));
    }

    [Fact, Trait("Requirement", "FR-037")]
    public async Task GetAsync_KeyOf1024Characters_IsAccepted()
    {
        var cache = _h.Create(Policies.CacheAside());

        var result = await cache.GetAsync(new string('k', 1024), _h.Token);

        Assert.Equal(ReadStatus.NotFound, result.Status);
    }

    [Fact, Trait("Requirement", "FR-003")]
    public async Task GetAsync_PolicyHasNoReadStrategy_ThrowsInvalidOperationException()
    {
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteThrough));

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync(CacheHarness.Key, _h.Token));
    }

    [Fact, Trait("Requirement", "TD-005")]
    public async Task GetAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var cache = _h.Create(Policies.CacheAside());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.GetAsync(CacheHarness.Key, cancelled.Token));
    }

    [Fact, Trait("Requirement", "FR-033")]
    public async Task GetAsync_AnyKey_AddsPrefixForStore()
    {
        var cache = _h.Create(Policies.CacheAside());

        await cache.GetAsync(CacheHarness.Key, _h.Token);

        _h.Store.Verify(s => s.GetAsync(CacheHarness.FullKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-004"), Trait("Requirement", "FR-005")]
    public async Task GetAsync_CacheAsideHit_ReturnsFoundWithValue()
    {
        _h.StoreHas(_h.Entry());
        var cache = _h.Create(Policies.CacheAside());

        var result = await cache.GetAsync(CacheHarness.Key, _h.Token);

        Assert.Equal(ReadStatus.Found, result.Status);
        Assert.Same(_h.Value, result.Value);
    }

    [Fact, Trait("Requirement", "FR-004"), Trait("Requirement", "FR-005")]
    public async Task GetAsync_CacheAsideMiss_ReturnsNotFound()
    {
        _h.StoreHas(null);
        var cache = _h.Create(Policies.CacheAside());

        var result = await cache.GetAsync(CacheHarness.Key, _h.Token);

        Assert.Equal(ReadStatus.NotFound, result.Status);
        Assert.Null(result.Value);
    }

    [Fact, Trait("Requirement", "FR-005")]
    public async Task GetAsync_CacheAsideMiss_DoesNotCallLoader()
    {
        _h.StoreHas(null);
        var cache = _h.Create(Policies.CacheAside());

        await cache.GetAsync(CacheHarness.Key, _h.Token);

        _h.Loader.Verify(l => l.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact, Trait("Requirement", "FR-012")]
    public async Task GetAsync_CacheAsideStoreFails_ReturnsStoreUnavailable()
    {
        _h.StoreGetFails();
        var cache = _h.Create(Policies.CacheAside());

        var result = await cache.GetAsync(CacheHarness.Key, _h.Token);

        Assert.Equal(ReadStatus.Failed, result.Status);
        Assert.Equal(CacheErrorKind.StoreUnavailable, result.Error!.Kind);
    }

    [Fact, Trait("Requirement", "FR-016")]
    public async Task GetAsync_CacheAsideUnreadableEntry_ReturnsNotFoundAndLogsWarning()
    {
        _h.StoreHas(_h.Entry());
        _h.Serializer.Setup(s => s.Deserialize<Product>(It.IsAny<ReadOnlyMemory<byte>>())).Throws(new FormatException());
        var cache = _h.Create(Policies.CacheAside());

        var result = await cache.GetAsync(CacheHarness.Key, _h.Token);

        Assert.Equal(ReadStatus.NotFound, result.Status);
        _h.VerifyLogged(LogLevel.Warning);
    }

    [Fact, Trait("Requirement", "FR-007")]
    public async Task GetAsync_ReadThroughHit_DoesNotCallLoader()
    {
        _h.StoreHas(_h.Entry());
        var cache = _h.Create(Policies.ReadThrough());

        var result = await cache.GetAsync(CacheHarness.Key, _h.Token);

        Assert.Equal(ReadStatus.Found, result.Status);
        _h.Loader.Verify(l => l.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact, Trait("Requirement", "FR-007")]
    public async Task GetAsync_ReadThroughMiss_ReturnsLoadedValue()
    {
        _h.StoreHas(null);
        _h.LoaderReturns(_h.Value);
        var cache = _h.Create(Policies.ReadThrough());

        var result = await cache.GetAsync(CacheHarness.Key, _h.Token);

        Assert.Equal(ReadStatus.Found, result.Status);
        Assert.Same(_h.Value, result.Value);
    }

    [Fact, Trait("Requirement", "FR-007"), Trait("Requirement", "FR-034")]
    public async Task GetAsync_ReadThroughMiss_StoresValueWithDefaultTtl()
    {
        _h.StoreHas(null);
        _h.LoaderReturns(_h.Value);
        var cache = _h.Create(Policies.ReadThrough());

        await cache.GetAsync(CacheHarness.Key, _h.Token);

        _h.VerifyStored(TimeSpan.FromMinutes(5), Times.Once());
    }

    [Fact, Trait("Requirement", "FR-034")]
    public async Task GetAsync_ReadThroughMissWithPolicyTtl_StoresValueWithPolicyTtl()
    {
        _h.StoreHas(null);
        _h.LoaderReturns(_h.Value);
        _h.Options.Policies["Product"] = new CachePolicyOptions { DefaultTtl = TimeSpan.FromMinutes(2) };
        var cache = _h.Create(Policies.ReadThrough());

        await cache.GetAsync(CacheHarness.Key, _h.Token);

        _h.VerifyStored(TimeSpan.FromMinutes(2), Times.Once());
    }

    [Fact, Trait("Requirement", "FR-033")]
    public async Task GetAsync_ReadThroughMiss_PassesKeyWithoutPrefixToLoader()
    {
        _h.StoreHas(null);
        _h.LoaderReturns(_h.Value);
        var cache = _h.Create(Policies.ReadThrough());

        await cache.GetAsync(CacheHarness.Key, _h.Token);

        _h.Loader.Verify(l => l.LoadAsync(CacheHarness.Key, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-014")]
    public async Task GetAsync_ReadThroughLoaderReturnsNull_ReturnsNotFoundAndCachesNothing()
    {
        _h.StoreHas(null);
        _h.LoaderReturns(null);
        var cache = _h.Create(Policies.ReadThrough());

        var result = await cache.GetAsync(CacheHarness.Key, _h.Token);

        Assert.Equal(ReadStatus.NotFound, result.Status);
        _h.VerifyNotStored();
    }

    [Fact, Trait("Requirement", "FR-013")]
    public async Task GetAsync_ReadThroughLoaderThrows_ReturnsLoaderFailedWithoutRetrying()
    {
        var failure = new TimeoutException();
        _h.StoreHas(null);
        _h.Loader.Setup(l => l.LoadAsync(CacheHarness.Key, It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var cache = _h.Create(Policies.ReadThrough());

        var result = await cache.GetAsync(CacheHarness.Key, _h.Token);

        Assert.Equal(ReadStatus.Failed, result.Status);
        Assert.Equal(CacheErrorKind.LoaderFailed, result.Error!.Kind);
        Assert.Same(failure, result.Error.Exception);
        _h.Loader.Verify(l => l.LoadAsync(CacheHarness.Key, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-012")]
    public async Task GetAsync_ReadThroughStoreFails_FallsBackToLoaderAndLogsWarning()
    {
        _h.StoreGetFails();
        _h.LoaderReturns(_h.Value);
        var cache = _h.Create(Policies.ReadThrough());

        var result = await cache.GetAsync(CacheHarness.Key, _h.Token);

        Assert.Equal(ReadStatus.Found, result.Status);
        Assert.Same(_h.Value, result.Value);
        _h.VerifyLogged(LogLevel.Warning);
    }

    [Fact, Trait("Requirement", "FR-012")]
    public async Task GetAsync_ReadThroughStoreFailsOnlyOnStore_ReturnsFoundAndLogsWarning()
    {
        _h.StoreHas(null);
        _h.LoaderReturns(_h.Value);
        _h.Store.Setup(s => s.SetAsync(CacheHarness.FullKey, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis is down"));
        var cache = _h.Create(Policies.ReadThrough());

        var result = await cache.GetAsync(CacheHarness.Key, _h.Token);

        Assert.Equal(ReadStatus.Found, result.Status);
        _h.VerifyLogged(LogLevel.Warning);
    }

    [Fact, Trait("Requirement", "FR-012"), Trait("Requirement", "FR-013")]
    public async Task GetAsync_ReadThroughStoreAndLoaderFail_ReturnsLoaderFailed()
    {
        _h.StoreGetFails();
        _h.Loader.Setup(l => l.LoadAsync(CacheHarness.Key, It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());
        var cache = _h.Create(Policies.ReadThrough());

        var result = await cache.GetAsync(CacheHarness.Key, _h.Token);

        Assert.Equal(ReadStatus.Failed, result.Status);
        Assert.Equal(CacheErrorKind.LoaderFailed, result.Error!.Kind);
    }

    [Fact, Trait("Requirement", "FR-016")]
    public async Task GetAsync_ReadThroughUnreadableEntry_LoadsFromLoader()
    {
        _h.StoreHas(_h.Entry());
        _h.Serializer.SetupSequence(s => s.Deserialize<Product>(It.IsAny<ReadOnlyMemory<byte>>())).Throws(new FormatException());
        _h.LoaderReturns(_h.Value);
        var cache = _h.Create(Policies.ReadThrough());

        var result = await cache.GetAsync(CacheHarness.Key, _h.Token);

        Assert.Equal(ReadStatus.Found, result.Status);
        _h.Loader.Verify(l => l.LoadAsync(CacheHarness.Key, It.IsAny<CancellationToken>()), Times.Once);
        _h.VerifyLogged(LogLevel.Warning);
    }

    [Fact, Trait("Requirement", "FR-015")]
    public async Task GetAsync_ConcurrentMissesOnOneKey_CallLoaderOnce()
    {
        var load = new TaskCompletionSource<Product?>();
        _h.StoreHas(null);
        _h.Loader.Setup(l => l.LoadAsync(CacheHarness.Key, It.IsAny<CancellationToken>())).Returns(load.Task);
        var cache = _h.Create(Policies.ReadThrough());

        var first = cache.GetAsync(CacheHarness.Key, _h.Token);
        var second = cache.GetAsync(CacheHarness.Key, _h.Token);
        load.SetResult(_h.Value);
        var results = await Task.WhenAll(first, second);

        Assert.All(results, r => Assert.Same(_h.Value, r.Value));
        _h.Loader.Verify(l => l.LoadAsync(CacheHarness.Key, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "TD-005"), Trait("Requirement", "FR-015")]
    public async Task GetAsync_OneCallerCancelsSharedLoad_OtherCallerStillGetsValue()
    {
        var load = new TaskCompletionSource<Product?>();
        using var cancel = new CancellationTokenSource();
        _h.StoreHas(null);
        _h.Loader.Setup(l => l.LoadAsync(CacheHarness.Key, It.IsAny<CancellationToken>())).Returns(load.Task);
        var cache = _h.Create(Policies.ReadThrough());

        var cancelled = cache.GetAsync(CacheHarness.Key, cancel.Token);
        var other = cache.GetAsync(CacheHarness.Key, _h.Token);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        load.SetResult(_h.Value);

        Assert.Same(_h.Value, (await other).Value);
    }

    [Fact, Trait("Requirement", "FR-008")]
    public async Task GetAsync_EntryBeforeThreshold_DoesNotStartRefresh()
    {
        _h.StoreHas(_h.Entry(age: TimeSpan.FromMinutes(3)));
        var cache = _h.Create(Policies.ReadThrough(refreshAhead: true));

        await cache.GetAsync(CacheHarness.Key, _h.Token);

        _h.Background.Verify(b => b.Run(It.IsAny<Func<CancellationToken, Task>>()), Times.Never);
    }

    [Fact, Trait("Requirement", "FR-008"), Trait("Requirement", "FR-009")]
    public async Task GetAsync_EntryExactlyAtThreshold_DoesNotStartRefresh()
    {
        _h.StoreHas(_h.Entry(age: TimeSpan.FromSeconds(225)));
        var cache = _h.Create(Policies.ReadThrough(refreshAhead: true));

        await cache.GetAsync(CacheHarness.Key, _h.Token);

        _h.Background.Verify(b => b.Run(It.IsAny<Func<CancellationToken, Task>>()), Times.Never);
    }

    [Fact, Trait("Requirement", "FR-008")]
    public async Task GetAsync_EntryPastThreshold_ReturnsCurrentValueAndStartsRefresh()
    {
        _h.StoreHas(_h.Entry(age: TimeSpan.FromMinutes(4)));
        var cache = _h.Create(Policies.ReadThrough(refreshAhead: true));

        var result = await cache.GetAsync(CacheHarness.Key, _h.Token);

        Assert.Same(_h.Value, result.Value);
        _h.Background.Verify(b => b.Run(It.IsAny<Func<CancellationToken, Task>>()), Times.Once);
        _h.Loader.Verify(l => l.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact, Trait("Requirement", "FR-008")]
    public async Task GetAsync_RefreshRuns_LoadsAndStoresNewValue()
    {
        Func<CancellationToken, Task>? refresh = null;
        _h.StoreHas(_h.Entry(age: TimeSpan.FromMinutes(4)));
        _h.LoaderReturns(_h.Value);
        _h.Background.Setup(b => b.Run(It.IsAny<Func<CancellationToken, Task>>())).Callback<Func<CancellationToken, Task>>(w => refresh = w);
        var cache = _h.Create(Policies.ReadThrough(refreshAhead: true));

        await cache.GetAsync(CacheHarness.Key, _h.Token);
        await refresh!(_h.Token);

        _h.Loader.Verify(l => l.LoadAsync(CacheHarness.Key, It.IsAny<CancellationToken>()), Times.Once);
        _h.VerifyStored(TimeSpan.FromMinutes(5), Times.Once());
    }

    [Fact, Trait("Requirement", "FR-008")]
    public async Task GetAsync_ReadThroughWithoutRefreshAhead_DoesNotStartRefresh()
    {
        _h.StoreHas(_h.Entry(age: TimeSpan.FromMinutes(4)));
        var cache = _h.Create(Policies.ReadThrough());

        await cache.GetAsync(CacheHarness.Key, _h.Token);

        _h.Background.Verify(b => b.Run(It.IsAny<Func<CancellationToken, Task>>()), Times.Never);
    }

    [Fact, Trait("Requirement", "FR-009")]
    public async Task GetAsync_GeneralThresholdChanged_UsesIt()
    {
        _h.Options.RefreshThreshold = 0.5;
        _h.StoreHas(_h.Entry(age: TimeSpan.FromMinutes(3)));
        var cache = _h.Create(Policies.ReadThrough(refreshAhead: true));

        await cache.GetAsync(CacheHarness.Key, _h.Token);

        _h.Background.Verify(b => b.Run(It.IsAny<Func<CancellationToken, Task>>()), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-009")]
    public async Task GetAsync_PolicyThresholdSet_OverridesGeneralThreshold()
    {
        _h.Options.Policies["Product"] = new CachePolicyOptions { RefreshThreshold = 0.9 };
        _h.StoreHas(_h.Entry(age: TimeSpan.FromMinutes(4)));
        var cache = _h.Create(Policies.ReadThrough(refreshAhead: true));

        await cache.GetAsync(CacheHarness.Key, _h.Token);

        _h.Background.Verify(b => b.Run(It.IsAny<Func<CancellationToken, Task>>()), Times.Never);
    }

    [Fact, Trait("Requirement", "FR-010")]
    public async Task GetAsync_KeyHasQueuedWrite_SkipsRefresh()
    {
        _h.StoreHas(_h.Entry(age: TimeSpan.FromMinutes(4), queued: true));
        var cache = _h.Create(Policies.ReadThrough(refreshAhead: true));

        var result = await cache.GetAsync(CacheHarness.Key, _h.Token);

        Assert.Same(_h.Value, result.Value);
        _h.Background.Verify(b => b.Run(It.IsAny<Func<CancellationToken, Task>>()), Times.Never);
    }

    [Fact, Trait("Requirement", "FR-011")]
    public async Task GetAsync_RefreshLoaderThrows_KeepsCurrentValueAndLogsWarning()
    {
        Func<CancellationToken, Task>? refresh = null;
        _h.StoreHas(_h.Entry(age: TimeSpan.FromMinutes(4)));
        _h.Loader.Setup(l => l.LoadAsync(CacheHarness.Key, It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());
        _h.Background.Setup(b => b.Run(It.IsAny<Func<CancellationToken, Task>>())).Callback<Func<CancellationToken, Task>>(w => refresh = w);
        var cache = _h.Create(Policies.ReadThrough(refreshAhead: true));

        await cache.GetAsync(CacheHarness.Key, _h.Token);
        await refresh!(_h.Token);

        _h.VerifyNotStored();
        _h.Store.Verify(s => s.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _h.VerifyLogged(LogLevel.Warning);
    }

    [Fact, Trait("Requirement", "FR-011")]
    public async Task GetAsync_ReadAfterFailedRefresh_TriesAgain()
    {
        Func<CancellationToken, Task>? refresh = null;
        _h.StoreHas(_h.Entry(age: TimeSpan.FromMinutes(4)));
        _h.Loader.Setup(l => l.LoadAsync(CacheHarness.Key, It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());
        _h.Background.Setup(b => b.Run(It.IsAny<Func<CancellationToken, Task>>())).Callback<Func<CancellationToken, Task>>(w => refresh = w);
        var cache = _h.Create(Policies.ReadThrough(refreshAhead: true));

        await cache.GetAsync(CacheHarness.Key, _h.Token);
        await refresh!(_h.Token);
        await cache.GetAsync(CacheHarness.Key, _h.Token);

        _h.Background.Verify(b => b.Run(It.IsAny<Func<CancellationToken, Task>>()), Times.Exactly(2));
    }

    [Fact, Trait("Requirement", "FR-015")]
    public async Task GetAsync_ReadsWhileRefreshRunning_StartOneRefresh()
    {
        _h.StoreHas(_h.Entry(age: TimeSpan.FromMinutes(4)));
        var cache = _h.Create(Policies.ReadThrough(refreshAhead: true));

        await cache.GetAsync(CacheHarness.Key, _h.Token);
        await cache.GetAsync(CacheHarness.Key, _h.Token);

        _h.Background.Verify(b => b.Run(It.IsAny<Func<CancellationToken, Task>>()), Times.Once);
    }

    [Fact, Trait("Requirement", "NFR-008")]
    public async Task GetAsync_Hit_RecordsHit()
    {
        _h.StoreHas(_h.Entry());
        var cache = _h.Create(Policies.CacheAside());

        await cache.GetAsync(CacheHarness.Key, _h.Token);

        _h.Metrics.Verify(m => m.RecordHit("Product"), Times.Once);
    }

    [Fact, Trait("Requirement", "NFR-008")]
    public async Task GetAsync_Miss_RecordsMiss()
    {
        _h.StoreHas(null);
        var cache = _h.Create(Policies.CacheAside());

        await cache.GetAsync(CacheHarness.Key, _h.Token);

        _h.Metrics.Verify(m => m.RecordMiss("Product"), Times.Once);
    }

    [Fact, Trait("Requirement", "NFR-008")]
    public async Task GetAsync_StoreFails_RecordsError()
    {
        _h.StoreGetFails();
        var cache = _h.Create(Policies.CacheAside());

        await cache.GetAsync(CacheHarness.Key, _h.Token);

        _h.Metrics.Verify(m => m.RecordError("Product", CacheErrorKind.StoreUnavailable), Times.Once);
    }
}
