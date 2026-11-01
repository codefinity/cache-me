using CacheMe.Tests.Support;
using Microsoft.Extensions.Logging;
using Moq;

namespace CacheMe.Tests;

public sealed class WriteBehindProcessorTests
{
    private readonly CacheHarness _h = new();

    private void Claims(params QueuedWrite[] writes) =>
        _h.Store.SetupSequence(s => s.ClaimAsync(It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(writes)
            .ReturnsAsync([]);

    private void WriterFails() =>
        _h.Writer.Setup(w => w.WriteAsync(It.IsAny<string>(), It.IsAny<Product>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException("Database timeout"));

    [Fact, Trait("Requirement", "FR-022")]
    public async Task FlushOnceAsync_NothingDue_ReturnsZero()
    {
        Claims();
        var processor = _h.CreateProcessor();

        var count = await processor.FlushOnceAsync(_h.Token);

        Assert.Equal(0, count);
        _h.Writer.Verify(w => w.WriteAsync(It.IsAny<string>(), It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact, Trait("Requirement", "FR-022")]
    public async Task FlushOnceAsync_FewerThanBatchWaiting_ClaimsOnlyWritesPastMaxDelay()
    {
        _h.Options.WriteBehind.MaxBatchSize = 50;
        _h.Options.WriteBehind.MaxFlushDelay = TimeSpan.FromSeconds(5);
        _h.Store.Setup(s => s.GetQueueLengthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(49);
        Claims();
        var processor = _h.CreateProcessor();

        await processor.FlushOnceAsync(_h.Token);

        _h.Store.Verify(s => s.ClaimAsync(50, _h.Clock.Now - TimeSpan.FromSeconds(5), _h.Clock.Now, It.Is<TimeSpan>(t => t > TimeSpan.Zero), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-022")]
    public async Task FlushOnceAsync_FullBatchWaiting_ClaimsWithoutWaitingForMaxDelay()
    {
        _h.Options.WriteBehind.MaxBatchSize = 50;
        _h.Store.Setup(s => s.GetQueueLengthAsync(It.IsAny<CancellationToken>())).ReturnsAsync(50);
        Claims();
        var processor = _h.CreateProcessor();

        await processor.FlushOnceAsync(_h.Token);

        _h.Store.Verify(s => s.ClaimAsync(50, _h.Clock.Now, _h.Clock.Now, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-020"), Trait("Requirement", "FR-033")]
    public async Task FlushOnceAsync_ClaimedWrite_CallsWriterWithKeyWithoutPrefix()
    {
        Claims(_h.Queued());
        var processor = _h.CreateProcessor();

        var count = await processor.FlushOnceAsync(_h.Token);

        Assert.Equal(1, count);
        _h.Writer.Verify(w => w.WriteAsync(CacheHarness.Key, _h.Value, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-023"), Trait("Requirement", "FR-027")]
    public async Task FlushOnceAsync_WriterSucceeds_CompletesWrite()
    {
        var write = _h.Queued();
        Claims(write);
        var processor = _h.CreateProcessor();

        await processor.FlushOnceAsync(_h.Token);

        _h.Store.Verify(s => s.CompleteAsync(write, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-022")]
    public async Task FlushOnceAsync_SeveralWrites_CallsWriterOncePerWrite()
    {
        Claims(_h.Queued("shop:product:1"), _h.Queued("shop:product:2"));
        var processor = _h.CreateProcessor();

        await processor.FlushOnceAsync(_h.Token);

        _h.Writer.Verify(w => w.WriteAsync("product:1", It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
        _h.Writer.Verify(w => w.WriteAsync("product:2", It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-025")]
    public async Task FlushOnceAsync_OneWriteFails_StillProcessesOthers()
    {
        var bad = _h.Queued("shop:product:1");
        var good = _h.Queued("shop:product:2");
        _h.Writer.Setup(w => w.WriteAsync("product:1", It.IsAny<Product>(), It.IsAny<CancellationToken>())).ThrowsAsync(new TimeoutException());
        Claims(bad, good);
        var processor = _h.CreateProcessor();

        await processor.FlushOnceAsync(_h.Token);

        _h.Store.Verify(s => s.CompleteAsync(good, It.IsAny<CancellationToken>()), Times.Once);
        _h.Store.Verify(s => s.CompleteAsync(bad, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact, Trait("Requirement", "FR-025")]
    public async Task FlushOnceAsync_FirstTryFails_RetriesAfterInitialDelay()
    {
        var write = _h.Queued(attempts: 0);
        WriterFails();
        Claims(write);
        var processor = _h.CreateProcessor();

        await processor.FlushOnceAsync(_h.Token);

        _h.Store.Verify(s => s.RetryLaterAsync(write, _h.Clock.Now + TimeSpan.FromSeconds(1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-025")]
    public async Task FlushOnceAsync_ThirdTryFails_DoublesDelayTwice()
    {
        var write = _h.Queued(attempts: 2);
        WriterFails();
        Claims(write);
        var processor = _h.CreateProcessor();

        await processor.FlushOnceAsync(_h.Token);

        _h.Store.Verify(s => s.RetryLaterAsync(write, _h.Clock.Now + TimeSpan.FromSeconds(4), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-025")]
    public async Task FlushOnceAsync_DelayWouldPassMaxDelay_UsesMaxDelay()
    {
        _h.Options.WriteBehind.Retry.MaxAttempts = 10;
        _h.Options.WriteBehind.Retry.MaxDelay = TimeSpan.FromSeconds(30);
        var write = _h.Queued(attempts: 7);
        WriterFails();
        Claims(write);
        var processor = _h.CreateProcessor();

        await processor.FlushOnceAsync(_h.Token);

        _h.Store.Verify(s => s.RetryLaterAsync(write, _h.Clock.Now + TimeSpan.FromSeconds(30), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-025"), Trait("Requirement", "FR-026")]
    public async Task FlushOnceAsync_FinalTryFails_MovesToDeadLetterWithRetention()
    {
        var write = _h.Queued(attempts: 4);
        WriterFails();
        Claims(write);
        var processor = _h.CreateProcessor();

        await processor.FlushOnceAsync(_h.Token);

        _h.Store.Verify(s => s.MoveToDeadLetterAsync(write, It.Is<string>(r => r.Length > 0), _h.Clock.Now, TimeSpan.FromDays(7), It.IsAny<CancellationToken>()), Times.Once);
        _h.Store.Verify(s => s.RetryLaterAsync(It.IsAny<QueuedWrite>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact, Trait("Requirement", "FR-025")]
    public async Task FlushOnceAsync_FinalTryFails_LogsError()
    {
        WriterFails();
        Claims(_h.Queued(attempts: 4));
        var processor = _h.CreateProcessor();

        await processor.FlushOnceAsync(_h.Token);

        CacheHarness.VerifyLogged(_h.ProcessorLogger, LogLevel.Error);
    }

    [Fact, Trait("Requirement", "FR-025")]
    public async Task FlushOnceAsync_FinalTryFails_NotifiesHandler()
    {
        WriterFails();
        Claims(_h.Queued(attempts: 4));
        var processor = _h.CreateProcessor();

        await processor.FlushOnceAsync(_h.Token);

        _h.DeadLetterHandler.Verify(d => d.HandleAsync(It.Is<DeadLetterWrite>(w => w.Key == CacheHarness.FullKey && w.Attempts == 5), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-026")]
    public async Task FlushOnceAsync_HandlerThrows_LogsAndKeepsDeadLetterWrite()
    {
        var write = _h.Queued(attempts: 4);
        WriterFails();
        _h.DeadLetterHandler.Setup(d => d.HandleAsync(It.IsAny<DeadLetterWrite>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException());
        Claims(write);
        var processor = _h.CreateProcessor();

        await processor.FlushOnceAsync(_h.Token);

        _h.Store.Verify(s => s.MoveToDeadLetterAsync(write, It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        _h.Store.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        CacheHarness.VerifyLogged(_h.ProcessorLogger, LogLevel.Error);
    }

    [Fact, Trait("Requirement", "FR-025")]
    public async Task FlushOnceAsync_QueuedValueCannotBeRead_MovesToDeadLetterWithoutRetrying()
    {
        var write = _h.Queued();
        _h.Serializer.Setup(s => s.Deserialize<Product>(It.IsAny<ReadOnlyMemory<byte>>())).Throws(new FormatException());
        Claims(write);
        var processor = _h.CreateProcessor();

        await processor.FlushOnceAsync(_h.Token);

        _h.Store.Verify(s => s.MoveToDeadLetterAsync(write, It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        _h.Store.Verify(s => s.RetryLaterAsync(It.IsAny<QueuedWrite>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact, Trait("Requirement", "FR-025")]
    public async Task FlushOnceAsync_QueuedValueTypeNotRegistered_MovesToDeadLetter()
    {
        var write = _h.Queued(valueType: "RemovedType");
        Claims(write);
        var processor = _h.CreateProcessor();

        await processor.FlushOnceAsync(_h.Token);

        _h.Store.Verify(s => s.MoveToDeadLetterAsync(write, It.IsAny<string>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact, Trait("Requirement", "NFR-008")]
    public async Task FlushOnceAsync_WriterThrows_RecordsError()
    {
        WriterFails();
        Claims(_h.Queued());
        var processor = _h.CreateProcessor();

        await processor.FlushOnceAsync(_h.Token);

        _h.Metrics.Verify(m => m.RecordError("Product", CacheErrorKind.WriterFailed), Times.Once);
    }

    [Fact, Trait("Requirement", "FR-028")]
    public async Task FlushAllAsync_WritesQueued_FlushesUntilEmpty()
    {
        _h.Store.SetupSequence(s => s.ClaimAsync(It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([_h.Queued("shop:product:1")])
            .ReturnsAsync([_h.Queued("shop:product:2")])
            .ReturnsAsync([]);
        var processor = _h.CreateProcessor();

        await processor.FlushAllAsync(_h.Token);

        _h.Writer.Verify(w => w.WriteAsync(It.IsAny<string>(), It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact, Trait("Requirement", "FR-028")]
    public async Task FlushAllAsync_AnyWrite_DoesNotWaitForMaxFlushDelay()
    {
        Claims();
        var processor = _h.CreateProcessor();

        await processor.FlushAllAsync(_h.Token);

        _h.Store.Verify(s => s.ClaimAsync(It.IsAny<int>(), _h.Clock.Now, _h.Clock.Now, It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact, Trait("Requirement", "FR-028")]
    public async Task FlushAllAsync_ShutdownTimeoutPasses_Stops()
    {
        _h.Options.WriteBehind.ShutdownTimeout = TimeSpan.FromSeconds(10);
        _h.Store.Setup(s => s.ClaimAsync(It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback(() => _h.Clock.Now += TimeSpan.FromSeconds(6))
            .ReturnsAsync([_h.Queued()]);
        var processor = _h.CreateProcessor();

        await processor.FlushAllAsync(_h.Token);

        _h.Store.Verify(s => s.ClaimAsync(It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
