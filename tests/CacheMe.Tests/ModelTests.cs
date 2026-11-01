using CacheMe.Tests.Support;

namespace CacheMe.Tests;

public sealed class ModelTests
{
    [Fact, Trait("Requirement", "FR-004")]
    public void Found_AnyValue_SetsStatusAndValue()
    {
        var value = new Product();

        var result = ReadResult<Product>.Found(value);

        Assert.Equal(ReadStatus.Found, result.Status);
        Assert.Same(value, result.Value);
        Assert.Null(result.Error);
    }

    [Fact, Trait("Requirement", "FR-004")]
    public void NotFound_NoArguments_SetsStatusOnly()
    {
        var result = ReadResult<Product>.NotFound();

        Assert.Equal(ReadStatus.NotFound, result.Status);
        Assert.Null(result.Value);
        Assert.Null(result.Error);
    }

    [Fact, Trait("Requirement", "FR-004")]
    public void ReadFailed_AnyError_SetsStatusAndError()
    {
        var error = new CacheError(CacheErrorKind.LoaderFailed, "Load failed");

        var result = ReadResult<Product>.Failed(error);

        Assert.Equal(ReadStatus.Failed, result.Status);
        Assert.Same(error, result.Error);
        Assert.Null(result.Value);
    }

    [Fact, Trait("Requirement", "TD-007")]
    public void Success_NoArguments_SetsSucceeded()
    {
        var result = WriteResult.Success();

        Assert.True(result.Succeeded);
        Assert.Null(result.Error);
    }

    [Fact, Trait("Requirement", "TD-007")]
    public void WriteFailed_AnyError_SetsError()
    {
        var error = new CacheError(CacheErrorKind.WriterFailed, "Write failed");

        var result = WriteResult.Failed(error);

        Assert.False(result.Succeeded);
        Assert.Same(error, result.Error);
    }

    [Fact, Trait("Requirement", "TD-007")]
    public void CacheErrorCtor_AllArguments_StoresThem()
    {
        var exception = new TimeoutException();

        var error = new CacheError(CacheErrorKind.StoreUnavailable, "Down", exception);

        Assert.Equal(CacheErrorKind.StoreUnavailable, error.Kind);
        Assert.Equal("Down", error.Message);
        Assert.Same(exception, error.Exception);
    }

    [Fact, Trait("Requirement", "FR-009"), Trait("Requirement", "FR-022"), Trait("Requirement", "FR-025"), Trait("Requirement", "FR-026"), Trait("Requirement", "FR-028"), Trait("Requirement", "FR-029"), Trait("Requirement", "NFR-002"), Trait("Requirement", "NFR-003"), Trait("Requirement", "NFR-004")]
    public void CacheMeOptions_NoSettings_HasApprovedDefaults()
    {
        var options = new CacheMeOptions();

        Assert.Equal("", options.KeyPrefix);
        Assert.Null(options.DefaultTtl);
        Assert.Equal(TimeSpan.FromMinutes(5), options.MaxTtl);
        Assert.Equal(0.75, options.RefreshThreshold);
        Assert.Equal(1_048_576, options.MaxValueBytes);
        Assert.Null(options.Redis.ConnectionString);
        Assert.Equal(TimeSpan.FromSeconds(1), options.Redis.OperationTimeout);
        Assert.Equal(TimeSpan.FromSeconds(5), options.WriteBehind.MaxFlushDelay);
        Assert.Equal(100, options.WriteBehind.MaxBatchSize);
        Assert.Equal(100_000, options.WriteBehind.MaxQueueLength);
        Assert.Equal(TimeSpan.FromSeconds(10), options.WriteBehind.ShutdownTimeout);
        Assert.Equal(5, options.WriteBehind.Retry.MaxAttempts);
        Assert.Equal(TimeSpan.FromSeconds(1), options.WriteBehind.Retry.InitialDelay);
        Assert.Equal(TimeSpan.FromSeconds(30), options.WriteBehind.Retry.MaxDelay);
        Assert.Equal(TimeSpan.FromDays(7), options.WriteBehind.DeadLetter.Retention);
    }

    [Fact, Trait("Requirement", "TD-004")]
    public void CacheMeOptions_PolicyKeys_AreCaseInsensitive()
    {
        var options = new CacheMeOptions();

        options.Policies["Product"] = new CachePolicyOptions();

        Assert.True(options.Policies.ContainsKey("PRODUCT"));
    }

    [Fact, Trait("Requirement", "NFR-008"), Trait("Requirement", "TD-008")]
    public void CacheMeMetrics_Names_MatchApprovedContract()
    {
        Assert.Equal("CacheMe", CacheMeMetrics.MeterName);
        Assert.Equal("cacheme.hits", CacheMeMetrics.Hits);
        Assert.Equal("cacheme.misses", CacheMeMetrics.Misses);
        Assert.Equal("cacheme.errors", CacheMeMetrics.Errors);
        Assert.Equal("cacheme.write_behind.queue_depth", CacheMeMetrics.QueueDepth);
    }
}
