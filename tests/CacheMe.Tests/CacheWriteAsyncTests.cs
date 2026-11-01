using CacheMe.Tests.Support;
using Microsoft.Extensions.Logging;
using Moq;

namespace CacheMe.Tests;

public sealed class CacheWriteAsyncTests
{
    private readonly CacheHarness _h = new();

    private void WriterFails() =>
        _h.Writer.Setup(w => w.WriteAsync(CacheHarness.Key, It.IsAny<Product>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());

    private void StoreSetFails() =>
        _h.Store.Setup(s => s.SetAsync(CacheHarness.FullKey, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis is down"));

    [Fact, Trait("Requirement", "FR-037")]
    public async Task WriteAsync_NullKey_ThrowsArgumentNullException()
    {
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteThrough));

        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.WriteAsync(null!, _h.Value, cancellationToken: _h.Token));
    }

    [Fact, Trait("Requirement", "FR-037")]
    public async Task WriteAsync_NullValue_ThrowsArgumentNullException()
    {
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteThrough));

        await Assert.ThrowsAsync<ArgumentNullException>(() => cache.WriteAsync(CacheHarness.Key, null!, cancellationToken: _h.Token));
    }

    [Fact, Trait("Requirement", "FR-037")]
    public async Task WriteAsync_EmptyKey_ThrowsArgumentException()
    {
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteThrough));

        await Assert.ThrowsAsync<ArgumentException>(() => cache.WriteAsync("", _h.Value, cancellationToken: _h.Token));
    }

    [Fact, Trait("Requirement", "FR-037")]
    public async Task WriteAsync_KeyOver1024Characters_ThrowsArgumentException()
    {
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteThrough));

        await Assert.ThrowsAsync<ArgumentException>(() => cache.WriteAsync(new string('k', 1025), _h.Value, cancellationToken: _h.Token));
    }

    [Theory, Trait("Requirement", "FR-037")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task WriteAsync_NonPositiveTtl_ThrowsArgumentOutOfRangeException(int seconds)
    {
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteThrough));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cache.WriteAsync(CacheHarness.Key, _h.Value, TimeSpan.FromSeconds(seconds), _h.Token));
    }

    [Fact, Trait("Requirement", "NFR-002")]
    public async Task WriteAsync_TtlOverMaxTtl_ThrowsArgumentOutOfRangeException()
    {
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteThrough));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cache.WriteAsync(CacheHarness.Key, _h.Value, TimeSpan.FromMinutes(6), _h.Token));
    }

    [Fact, Trait("Requirement", "FR-003")]
    public async Task WriteAsync_PolicyHasNoWriteStrategy_ThrowsInvalidOperationException()
    {
        var cache = _h.Create(Policies.ReadThrough());

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token));
    }

    [Fact, Trait("Requirement", "TD-005")]
    public async Task WriteAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteThrough));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: cancelled.Token));
    }

    [Fact, Trait("Requirement", "FR-017")]
    public async Task WriteAsync_WriteThrough_WritesDatabaseBeforeCache()
    {
        var order = new List<string>();
        _h.Writer.Setup(w => w.WriteAsync(CacheHarness.Key, _h.Value, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("database")).Returns(Task.CompletedTask);
        _h.Store.Setup(s => s.SetAsync(CacheHarness.FullKey, It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("cache")).Returns(Task.CompletedTask);
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteThrough));

        var result = await cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "database", "cache" }, order);
    }

    [Fact, Trait("Requirement", "FR-034")]
    public async Task WriteAsync_WriteThroughTtlGiven_StoresWithIt()
    {
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteThrough));

        await cache.WriteAsync(CacheHarness.Key, _h.Value, TimeSpan.FromMinutes(1), _h.Token);

        _h.VerifyStored(TimeSpan.FromMinutes(1), Times.Once());
    }

    [Fact, Trait("Requirement", "FR-018")]
    public async Task WriteAsync_WriteThroughWriterThrows_ReturnsWriterFailedAndLeavesCache()
    {
        WriterFails();
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteThrough));

        var result = await cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.Equal(CacheErrorKind.WriterFailed, result.Error!.Kind);
        _h.VerifyNotStored();
    }

    [Fact, Trait("Requirement", "FR-019")]
    public async Task WriteAsync_WriteThroughCacheFails_ReportsSuccessLogsWarningAndRemovesOldEntry()
    {
        StoreSetFails();
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteThrough));

        var result = await cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.True(result.Succeeded);
        _h.VerifyLogged(LogLevel.Warning);
        _h.Store.Verify(s => s.RemoveAsync(CacheHarness.FullKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-019")]
    public async Task WriteAsync_WriteThroughCacheAndRemoveFail_StillReportsSuccess()
    {
        StoreSetFails();
        _h.Store.Setup(s => s.RemoveAsync(CacheHarness.FullKey, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Redis is down"));
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteThrough));

        var result = await cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.True(result.Succeeded);
    }

    [Fact, Trait("Requirement", "FR-038")]
    public async Task WriteAsync_WriteThroughValueTooLarge_ReturnsErrorBeforeWritingDatabase()
    {
        _h.Options.MaxValueBytes = 2;
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteThrough));

        var result = await cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.Equal(CacheErrorKind.ValueTooLarge, result.Error!.Kind);
        _h.Writer.Verify(w => w.WriteAsync(It.IsAny<string>(), It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact, Trait("Requirement", "TD-007")]
    public async Task WriteAsync_WriteThroughSerializerThrows_ReturnsErrorBeforeWritingDatabase()
    {
        _h.Serializer.Setup(s => s.Serialize(It.IsAny<Product>())).Throws(new NotSupportedException());
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteThrough));

        var result = await cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.Equal(CacheErrorKind.SerializationFailed, result.Error!.Kind);
        _h.Writer.Verify(w => w.WriteAsync(It.IsAny<string>(), It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact, Trait("Requirement", "FR-020")]
    public async Task WriteAsync_WriteBehind_QueuesWriteAndReportsSuccess()
    {
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteBehind));

        var result = await cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.True(result.Succeeded);
        _h.Store.Verify(s => s.TryEnqueueAsync(CacheHarness.FullKey, "Product", It.IsAny<ReadOnlyMemory<byte>>(), _h.Clock.Now, TimeSpan.FromMinutes(5), _h.Options.WriteBehind.MaxQueueLength, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-020")]
    public async Task WriteAsync_WriteBehind_DoesNotCallWriter()
    {
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteBehind));

        await cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        _h.Writer.Verify(w => w.WriteAsync(It.IsAny<string>(), It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact, Trait("Requirement", "FR-020"), Trait("Requirement", "FR-028")]
    public async Task WriteAsync_WriteBehindServiceNotRunning_ThrowsInvalidOperationException()
    {
        _h.Status.Setup(s => s.IsRunning).Returns(false);
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteBehind));

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token));
    }

    [Fact, Trait("Requirement", "FR-021")]
    public async Task WriteAsync_WriteBehindStoreFails_ReturnsStoreUnavailableAndDoesNotCallWriter()
    {
        _h.Store.Setup(s => s.TryEnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis is down"));
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteBehind));

        var result = await cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.Equal(CacheErrorKind.StoreUnavailable, result.Error!.Kind);
        _h.Writer.Verify(w => w.WriteAsync(It.IsAny<string>(), It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact, Trait("Requirement", "FR-029")]
    public async Task WriteAsync_WriteBehindQueueFull_ReturnsQueueFull()
    {
        _h.Store.Setup(s => s.TryEnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteBehind));

        var result = await cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.Equal(CacheErrorKind.QueueFull, result.Error!.Kind);
    }

    [Fact, Trait("Requirement", "FR-038")]
    public async Task WriteAsync_WriteBehindValueTooLarge_ReturnsErrorAndQueuesNothing()
    {
        _h.Options.MaxValueBytes = 2;
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteBehind));

        var result = await cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.Equal(CacheErrorKind.ValueTooLarge, result.Error!.Kind);
        _h.Store.Verify(s => s.TryEnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact, Trait("Requirement", "FR-030")]
    public async Task WriteAsync_WriteAround_WritesDatabaseThenRemovesEntry()
    {
        var order = new List<string>();
        _h.Writer.Setup(w => w.WriteAsync(CacheHarness.Key, _h.Value, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("database")).Returns(Task.CompletedTask);
        _h.Store.Setup(s => s.RemoveAsync(CacheHarness.FullKey, It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("remove")).ReturnsAsync(true);
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteAround));

        var result = await cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "database", "remove" }, order);
    }

    [Fact, Trait("Requirement", "FR-030")]
    public async Task WriteAsync_WriteAround_StoresNothingInCache()
    {
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteAround));

        await cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        _h.VerifyNotStored();
    }

    [Fact, Trait("Requirement", "FR-030")]
    public async Task WriteAsync_WriteAroundWriterThrows_ReturnsWriterFailedAndKeepsEntry()
    {
        WriterFails();
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteAround));

        var result = await cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.Equal(CacheErrorKind.WriterFailed, result.Error!.Kind);
        _h.Store.Verify(s => s.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact, Trait("Requirement", "FR-031")]
    public async Task WriteAsync_WriteAroundRemoveFails_ReportsSuccessAndLogsWarning()
    {
        _h.Store.Setup(s => s.RemoveAsync(CacheHarness.FullKey, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("Redis is down"));
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteAround));

        var result = await cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.True(result.Succeeded);
        _h.VerifyLogged(LogLevel.Warning);
    }

    [Fact, Trait("Requirement", "FR-038")]
    public async Task WriteAsync_WriteAroundValueOverMaxValueBytes_IsStillWritten()
    {
        _h.Options.MaxValueBytes = 2;
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteAround));

        var result = await cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        Assert.True(result.Succeeded);
        _h.Writer.Verify(w => w.WriteAsync(CacheHarness.Key, _h.Value, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-002")]
    public async Task WriteAsync_CacheAsideWithWriteThrough_FollowsWriteStrategy()
    {
        var cache = _h.Create(Policies.CacheAside(WriteStrategy.WriteThrough));

        await cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        _h.Writer.Verify(w => w.WriteAsync(CacheHarness.Key, _h.Value, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "NFR-008")]
    public async Task WriteAsync_WriterThrows_RecordsError()
    {
        WriterFails();
        var cache = _h.Create(Policies.Write(WriteStrategy.WriteThrough));

        await cache.WriteAsync(CacheHarness.Key, _h.Value, cancellationToken: _h.Token);

        _h.Metrics.Verify(m => m.RecordError("Product", CacheErrorKind.WriterFailed), Times.Once);
    }
}
