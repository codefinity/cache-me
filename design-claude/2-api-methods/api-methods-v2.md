# Library Public API

| Field       | Value                                                           |
|-------------|-----------------------------------------------------------------|
| Stage       | 2 – API methods                                                 |
| Version     | v2                                                              |
| Date        | 2026-11-01                                                      |
| Status      | Approved                                                        |
| Approved on | 2026-11-01                                                      |
| Based on    | analysis-v3.md (Approved), configuration-v4.md (Approved)       |
| Supersedes  | v1                                                              |

## Changelog
- v2: Recorded the answers to Q-001, Q-002 and Q-003. This confirms the design as written, so the API itself is unchanged.

## Public interfaces and classes

```csharp
namespace CacheMe;

// What a repository injects.
public interface ICache<T> where T : class
{
    Task<ReadResult<T>> GetAsync(string key, CancellationToken cancellationToken = default);
    Task<WriteResult> StoreInCacheAsync(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
    Task<WriteResult> WriteAsync(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);
    Task<WriteResult> RemoveAsync(string key, CancellationToken cancellationToken = default);
}

public sealed class Cache<T> : ICache<T> where T : class
{
    // Internal constructor, used by AddCacheMe and the tests. See Dependencies (seams).
}

// What the application implements to reach its database.
public interface ICacheLoader<T> where T : class
{
    Task<T?> LoadAsync(string key, CancellationToken cancellationToken);
}

public interface ICacheWriter<T> where T : class
{
    Task WriteAsync(string key, T value, CancellationToken cancellationToken);
}

// Optional replacements and hooks.
public interface ICacheSerializer
{
    byte[] Serialize<T>(T value) where T : class;
    T Deserialize<T>(ReadOnlyMemory<byte> data) where T : class;
}

public interface IDeadLetterHandler
{
    Task HandleAsync(DeadLetterWrite write, CancellationToken cancellationToken);
}

// Implemented to use another store. Redis is included.
public interface ICacheStore
{
    Task<CacheEntry?> GetAsync(string key, CancellationToken cancellationToken);
    Task SetAsync(string key, ReadOnlyMemory<byte> value, DateTimeOffset createdAt, TimeSpan ttl, CancellationToken cancellationToken);
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken);
}

public interface IDeadLetterStore
{
    Task<IReadOnlyList<DeadLetterWrite>> ListAsync(int maxCount, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
}

// Also implemented by a store that supports write-behind. Redis does.
public interface IWriteBehindStore : ICacheStore, IDeadLetterStore
{
    Task<bool> TryEnqueueAsync(string key, string valueType, ReadOnlyMemory<byte> value, DateTimeOffset queuedAt, TimeSpan ttl, int maxQueueLength, CancellationToken cancellationToken);
    Task<IReadOnlyList<QueuedWrite>> ClaimAsync(int maxCount, DateTimeOffset queuedBefore, DateTimeOffset now, TimeSpan claimDuration, CancellationToken cancellationToken);
    Task CompleteAsync(QueuedWrite write, CancellationToken cancellationToken);
    Task RetryLaterAsync(QueuedWrite write, DateTimeOffset nextAttemptAt, CancellationToken cancellationToken);
    Task MoveToDeadLetterAsync(QueuedWrite write, string failureReason, DateTimeOffset failedAt, TimeSpan retention, CancellationToken cancellationToken);
    Task<bool> CancelAsync(string key, CancellationToken cancellationToken);
    Task<long> GetQueueLengthAsync(CancellationToken cancellationToken);
}
```

## Members

Every async member takes a `CancellationToken`. Everything public is safe for concurrent use (NFR-005). Only `ICache<T>` members validate keys, values and TTLs.

### `Task<ReadResult<T>> ICache<T>.GetAsync(string key, CancellationToken cancellationToken = default)`
- **Purpose:** Reads a value using the type's read strategy.
- **Parameters:** `key`: the caller's key without the prefix, not empty, at most 1,024 characters. `cancellationToken`: stops the caller's wait.
- **Returns:** `ReadStatus.Found` with the value, `ReadStatus.NotFound`, or `ReadStatus.Failed` with a `CacheError`.
    - A cached entry that cannot be read counts as a miss (FR-016).
    - With cache-aside, a miss returns `NotFound`.
    - With read-through, a miss runs the loader once per key per process, and a null result returns `NotFound` without caching it.
    - If the store fails, read-through falls back to the loader and logs a warning. Cache-aside returns `Failed` with `StoreUnavailable`.
    - If the loader throws, the read returns `Failed` with `LoaderFailed`.
    - With refresh-ahead, a read past the threshold returns the current value and starts a background reload, unless the key has a queued write.
- **Exceptions:** `ArgumentNullException`: `key` is null. `ArgumentException`: `key` is empty or over 1,024 characters. `InvalidOperationException`: the policy has no read strategy. `OperationCanceledException`: the token was cancelled.
- **Async / cancellation:** Cancelling stops this caller's wait. A load shared with other callers, or a background reload, continues.
- **Thread-safety:** Safe for concurrent use.
- **Requirements:** FR-003, FR-004, FR-005, FR-007, FR-008, FR-010, FR-011, FR-012, FR-013, FR-014, FR-015, FR-016, FR-033, FR-037, TD-005

```csharp
ReadResult<Product> result = await cache.GetAsync("product:42", cancellationToken);
```

### `Task<WriteResult> ICache<T>.StoreInCacheAsync(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)`
- **Purpose:** Stores a value the repository has just loaded after a cache-aside miss, without triggering any write strategy.
- **Parameters:** `key`: as in `GetAsync`. `value`: not null. `ttl`: greater than zero and at most the effective `MaxTtl`; null uses the effective `DefaultTtl`. `cancellationToken`: stops the caller's wait.
- **Returns:** `WriteResult` with `Succeeded` true, or an error: `ValueTooLarge`, `SerializationFailed` or `StoreUnavailable`.
- **Exceptions:** `ArgumentNullException`: `key` or `value` is null. `ArgumentException`: `key` is empty or over 1,024 characters. `ArgumentOutOfRangeException`: `ttl` is out of range. `InvalidOperationException`: the type's read strategy is not cache-aside. `OperationCanceledException`: the token was cancelled.
- **Async / cancellation:** Cancelling stops the caller's wait, and the entry may or may not have been stored.
- **Thread-safety:** Safe for concurrent use.
- **Requirements:** FR-005, FR-006, FR-033, FR-034, FR-037, FR-038

```csharp
await cache.StoreInCacheAsync("country:IN", country, cancellationToken: cancellationToken);
```

### `Task<WriteResult> ICache<T>.WriteAsync(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)`
- **Purpose:** Writes a value using the type's write strategy.
- **Parameters:** As in `StoreInCacheAsync`. Write-around still validates `ttl`, but stores nothing in the cache.
- **Returns:** `WriteResult`. Success depends on the strategy:

| Strategy | `Succeeded` means | Errors |
|----------|-------------------|--------|
| Write-through | Writer finished, then the cache was updated. If the cache update fails, it still succeeds, logs a warning and tries to remove the old entry. | `WriterFailed`, `ValueTooLarge`, `SerializationFailed` |
| Write-behind | The value is in the cache and the database write is queued. | `StoreUnavailable`, `QueueFull`, `ValueTooLarge`, `SerializationFailed` |
| Write-around | Writer finished. If the entry cannot be removed, it still succeeds and logs a warning. | `WriterFailed` |

- **Exceptions:** `ArgumentNullException`: `key` or `value` is null. `ArgumentException`: `key` is empty or over 1,024 characters. `ArgumentOutOfRangeException`: `ttl` is out of range. `InvalidOperationException`: the policy has no write strategy, or write-behind is used while its background service is not running. `OperationCanceledException`: the token was cancelled.
- **Async / cancellation:** The token is passed to the writer. If the caller cancels after the database write has started, the write may still have completed, so the outcome is unknown. A queued write is not cancelled by this token.
- **Thread-safety:** Safe for concurrent use.
- **Requirements:** FR-003, FR-017, FR-018, FR-019, FR-020, FR-021, FR-029, FR-030, FR-031, FR-033, FR-034, FR-037, FR-038, TD-005

```csharp
WriteResult result = await cache.WriteAsync("product:42", product, cancellationToken: cancellationToken);
```

### `Task<WriteResult> ICache<T>.RemoveAsync(string key, CancellationToken cancellationToken = default)`
- **Purpose:** Removes an entry from the cache only, and cancels any queued write for the key. The database is not touched.
- **Parameters:** `key`: as in `GetAsync`. `cancellationToken`: stops the caller's wait.
- **Returns:** `WriteResult` with `Succeeded` true whether or not an entry existed, or a `StoreUnavailable` error.
- **Exceptions:** `ArgumentNullException`: `key` is null. `ArgumentException`: `key` is empty or over 1,024 characters. `OperationCanceledException`: the token was cancelled.
- **Async / cancellation:** Cancelling stops the caller's wait, and the entry may or may not have been removed.
- **Thread-safety:** Safe for concurrent use.
- **Requirements:** FR-032, FR-033, FR-037

```csharp
await cache.RemoveAsync("product:42", cancellationToken);
```

### `Task<T?> ICacheLoader<T>.LoadAsync(string key, CancellationToken cancellationToken)`
- **Purpose:** Application code that reads one value from the database.
- **Parameters:** `key`: the caller's key without the prefix. `cancellationToken`: cancelled at shutdown or when the shared load is abandoned.
- **Returns:** The value, or null if it does not exist.
- **Exceptions:** Any exception counts as a failed load (FR-011, FR-013). The library does not retry.
- **Async / cancellation:** Runs on the caller's path for a miss and in the background for a refresh.
- **Thread-safety:** A new instance is resolved in a new DI scope for each use, so it does not need to be thread-safe.
- **Requirements:** FR-007, FR-008, FR-011, FR-013, FR-014, TD-006

```csharp
public Task<Product?> LoadAsync(string key, CancellationToken cancellationToken) =>
    db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == ProductKeys.ParseId(key), cancellationToken);
```

### `Task ICacheWriter<T>.WriteAsync(string key, T value, CancellationToken cancellationToken)`
- **Purpose:** Application code that saves one value to the database.
- **Parameters:** `key`: the caller's key without the prefix. `value`: the value to save. `cancellationToken`: the caller's token, or a background token for write-behind.
- **Returns:** A task that completes when the value is saved.
- **Exceptions:** Any exception counts as a failed write. Write-through and write-around return `WriterFailed`, and write-behind retries.
- **Async / cancellation:** Runs on the caller's path, or in the background for write-behind. It runs outside the caller's database transaction (Q-031 in Stage 0).
- **Thread-safety:** A new instance is resolved in a new DI scope for each use. For write-behind, this is one scope and one call per queued write (Q-002).
- **Requirements:** FR-017, FR-018, FR-020, FR-022, FR-025, FR-030, TD-006

```csharp
public async Task WriteAsync(string key, Product value, CancellationToken cancellationToken)
{
    db.Products.Update(value);
    await db.SaveChangesAsync(cancellationToken);
}
```

### `Task IDeadLetterHandler.HandleAsync(DeadLetterWrite write, CancellationToken cancellationToken)`
- **Purpose:** Optional application code that is notified after a queued write moves to the dead-letter store.
- **Parameters:** `write`: the failed write, including its raw payload, which may not be readable after a type change. `cancellationToken`: cancelled at shutdown.
- **Returns:** A task that completes when handling is finished.
- **Exceptions:** Any exception is logged. The write stays in the dead-letter store until its retention ends (Q-085 in Stage 0).
- **Async / cancellation:** Runs in the background, after the write is stored.
- **Thread-safety:** A new instance is resolved in a new DI scope for each use.
- **Requirements:** FR-025, FR-026

```csharp
public Task HandleAsync(DeadLetterWrite write, CancellationToken cancellationToken)
{
    logger.LogError("Write {Id} for {ValueType} failed: {Reason}", write.Id, write.ValueType, write.FailureReason);
    return Task.CompletedTask;
}
```

### `ICacheSerializer`

Replaces the default `System.Text.Json` serializer. Members are synchronous and must be safe for concurrent use, because the serializer is a singleton. A serialize failure returns `SerializationFailed`. A deserialize failure on a cached entry counts as a miss (FR-016).

| Member | Purpose | Parameters | Returns | Exceptions | Requirements |
|--------|---------|------------|---------|------------|--------------|
| `byte[] Serialize<T>(T value)` | Turns a value into bytes | `value`: not null | The bytes to store | Any exception counts as a failed serialize | FR-035 |
| `T Deserialize<T>(ReadOnlyMemory<byte> data)` | Turns stored bytes back into a value | `data`: the stored bytes | The value | Any exception counts as unreadable data | FR-016, FR-035 |

```csharp
cache.UseSerializer<MyEncryptingSerializer>();
```

### `ICacheStore`

Implement this to use a store other than Redis. The store receives keys with the prefix already added. All members are singletons and must be safe for concurrent use. Any exception other than `OperationCanceledException` counts as a store failure, and timeouts are the store's responsibility. Members honour the token.

| Member | Purpose | Parameters | Returns | Requirements |
|--------|---------|------------|---------|--------------|
| `Task<CacheEntry?> GetAsync(string key, CancellationToken cancellationToken)` | Reads an entry | `key`: full key | The entry, or null if there is none | FR-005, FR-007, FR-036 |
| `Task SetAsync(string key, ReadOnlyMemory<byte> value, DateTimeOffset createdAt, TimeSpan ttl, CancellationToken cancellationToken)` | Stores an entry that expires after `ttl` | `createdAt`: used to work out the refresh threshold | A task | FR-006, FR-008, FR-034, FR-036 |
| `Task<bool> RemoveAsync(string key, CancellationToken cancellationToken)` | Removes an entry | `key`: full key | Whether an entry existed | FR-030, FR-032, FR-036 |

```csharp
cache.UseStore<MyCacheStore>();
```

### `IDeadLetterStore`

The application can inject this to inspect and clear failed writes. It is registered only when the store supports write-behind (Q-003). The async and thread-safety rules of `ICacheStore` apply. The library does not replay these writes (FR-026).

| Member | Purpose | Parameters | Returns | Requirements |
|--------|---------|------------|---------|--------------|
| `Task<IReadOnlyList<DeadLetterWrite>> ListAsync(int maxCount, CancellationToken cancellationToken)` | Lists stored failed writes, oldest first | `maxCount`: at least 1 | Up to `maxCount` writes | FR-026 |
| `Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)` | Deletes a failed write once it has been dealt with | `id`: `DeadLetterWrite.Id` | Whether it existed | FR-026 |

```csharp
IReadOnlyList<DeadLetterWrite> failed = await deadLetters.ListAsync(50, cancellationToken);
```

### `IWriteBehindStore`

A store that supports write-behind implements this too. These operations must be atomic, because many instances share the queue (NFR-006). The async and thread-safety rules of `ICacheStore` apply.

| Member | Purpose | Parameters | Returns | Requirements |
|--------|---------|------------|---------|--------------|
| `TryEnqueueAsync(...)` | In one step, stores the value with no expiry, replaces any queued value for the key, and queues the write | `valueType`: the policy's settings key. `ttl`: applied when the write completes. `maxQueueLength`: the limit | False if the queue is full and the key is not already queued | FR-020, FR-023, FR-027, FR-029 |
| `ClaimAsync(...)` | Claims up to `maxCount` due writes for one instance. A key has only one queued write, and it is claimed by one instance at a time. An unfinished claim becomes available again after `claimDuration`, so a crash loses nothing | `queuedBefore`: oldest age to claim. `now`: to check retry times | The claimed writes | FR-022, FR-024, NFR-007 |
| `CompleteAsync(...)` | Removes a finished write and applies its TTL to the entry, unless a newer write has replaced it | `write`: the claimed write | A task | FR-023, FR-027 |
| `RetryLaterAsync(...)` | Releases a failed write until `nextAttemptAt` and adds one to its attempts | `nextAttemptAt`: next try | A task | FR-025 |
| `MoveToDeadLetterAsync(...)` | Moves a write to the dead-letter store for `retention` and applies its TTL to the entry | `failureReason`: for the handler and operators | A task | FR-025, FR-026 |
| `CancelAsync(...)` | Removes the queued write for a key | `key`: full key | Whether one was queued | FR-032 |
| `GetQueueLengthAsync(...)` | Counts queued writes | — | The count | FR-029, NFR-008 |

### `CacheMeBuilder`

Used inside `AddCacheMe`, on one thread at startup. Members are synchronous and return the builder so calls can be chained. All exceptions are `InvalidOperationException`, raised during registration, as listed in Stage 1.

| Member | Purpose | Exceptions | Requirements |
|--------|---------|------------|--------------|
| `Configure(Action<CacheMeOptions> configure)` | Overrides settings in code, after JSON | — | TD-004 |
| `UseRedis()` | Uses Redis with `CacheMe:Redis:ConnectionString` | A second store is registered | FR-036, TD-009 |
| `UseRedis(string connectionString)` | Uses Redis with this connection string | A second store is registered | FR-036, TD-009 |
| `UseRedis(Func<IServiceProvider, IConnectionMultiplexer> connection)` | Uses the application's existing connection | A second store is registered | FR-036, TD-009 |
| `UseStore<TStore>() where TStore : class, ICacheStore` | Uses another store, registered as a singleton | A second store is registered, or a policy uses write-behind and `TStore` is not an `IWriteBehindStore` | FR-036 |
| `UseSerializer<TSerializer>() where TSerializer : class, ICacheSerializer` | Replaces the serializer | — | FR-035 |
| `UseDeadLetterHandler<THandler>() where THandler : class, IDeadLetterHandler` | Sets the handler, registered as scoped | — | FR-025 |
| `ForType<T>(Action<CachePolicyBuilder<T>> policy)` | Registers `ICache<T>` and its policy under `typeof(T).Name` | The type or settings key is already registered | FR-001 |
| `ForType<T>(string settingsKey, Action<CachePolicyBuilder<T>> policy)` | As above, with an explicit settings key | As above | FR-001 |

```csharp
cache.UseRedis();
cache.ForType<Product>(policy => policy.ReadThrough<ProductLoader>());
```

### `CachePolicyBuilder<T>`

Used inside `ForType`, on one thread at startup. Members are synchronous and return the builder. A policy must end up with at least one strategy, at most one read strategy and at most one write strategy, otherwise registration throws `InvalidOperationException`. Any two can be combined (FR-002).

| Member | Purpose | Exceptions | Requirements |
|--------|---------|------------|--------------|
| `CacheAside()` | Read strategy: the repository handles the miss | A read strategy is already set | FR-005 |
| `ReadThrough<TLoader>() where TLoader : class, ICacheLoader<T>` | Read strategy: the library calls the loader on a miss | A read strategy is already set | FR-007 |
| `RefreshAhead()` | Add-on: reloads in the background past the threshold | No read-through strategy is set | FR-008 |
| `WriteThrough<TWriter>() where TWriter : class, ICacheWriter<T>` | Write strategy: database, then cache | A write strategy is already set | FR-017 |
| `WriteBehind<TWriter>() where TWriter : class, ICacheWriter<T>` | Write strategy: cache and queue, database later | A write strategy is already set | FR-020 |
| `WriteAround<TWriter>() where TWriter : class, ICacheWriter<T>` | Write strategy: database, then remove the entry | A write strategy is already set | FR-030 |

```csharp
cache.ForType<Product>(policy => policy
    .ReadThrough<ProductLoader>()
    .RefreshAhead()
    .WriteThrough<ProductWriter>());
```

### `CacheMeMetrics`

Constants naming the metrics, for dashboards. All are tagged with `value_type`.

| Constant | Value | Instrument | Requirements |
|----------|-------|------------|--------------|
| `MeterName` | `CacheMe` | Meter | NFR-008, TD-008 |
| `Hits` | `cacheme.hits` | Counter | NFR-008 |
| `Misses` | `cacheme.misses` | Counter | NFR-008 |
| `Errors` | `cacheme.errors` | Counter, also tagged with `error_kind` | NFR-008 |
| `QueueDepth` | `cacheme.write_behind.queue_depth` | Observable gauge, with no `value_type` tag | NFR-008 |

## Models, enums and exception types

```csharp
public enum ReadStatus { Found, NotFound, Failed }

public sealed class ReadResult<T> where T : class
{
    public ReadStatus Status { get; }
    public T? Value { get; }            // Set only when Status is Found.
    public CacheError? Error { get; }   // Set only when Status is Failed.

    public static ReadResult<T> Found(T value);
    public static ReadResult<T> NotFound();
    public static ReadResult<T> Failed(CacheError error);
}

public sealed class WriteResult
{
    public bool Succeeded { get; }
    public CacheError? Error { get; }   // Set only when Succeeded is false.

    public static WriteResult Success();
    public static WriteResult Failed(CacheError error);
}

public enum CacheErrorKind
{
    StoreUnavailable,     // The store failed or timed out.
    LoaderFailed,         // The loader threw.
    WriterFailed,         // The writer threw.
    QueueFull,            // The write-behind queue is at MaxQueueLength.
    ValueTooLarge,        // The serialized value is over MaxValueBytes.
    SerializationFailed   // The serializer threw on a write.
}

public sealed class CacheError
{
    public CacheError(CacheErrorKind kind, string message, Exception? exception = null);

    public CacheErrorKind Kind { get; }
    public string Message { get; }
    public Exception? Exception { get; }
}

// What a store returns for a key.
public sealed class CacheEntry
{
    public required ReadOnlyMemory<byte> Value { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required TimeSpan Ttl { get; init; }
    public required bool HasQueuedWrite { get; init; }
}

// A claimed write-behind write.
public sealed class QueuedWrite
{
    public required string Key { get; init; }
    public required string ValueType { get; init; }
    public required ReadOnlyMemory<byte> Value { get; init; }
    public required TimeSpan Ttl { get; init; }
    public required DateTimeOffset FirstQueuedAt { get; init; }
    public required int Attempts { get; init; }
    public required string Version { get; init; }   // Changes when a newer value replaces this one.
}

// A write in the dead-letter store.
public sealed class DeadLetterWrite
{
    public required string Id { get; init; }
    public required string Key { get; init; }
    public required string ValueType { get; init; }
    public required ReadOnlyMemory<byte> Value { get; init; }
    public required int Attempts { get; init; }
    public required DateTimeOffset FirstQueuedAt { get; init; }
    public required DateTimeOffset FailedAt { get; init; }
    public required string FailureReason { get; init; }
}
```

**Custom exception types:** none. Runtime failures are result objects (TD-007). Programmer errors use `ArgumentNullException`, `ArgumentException`, `ArgumentOutOfRangeException` and `InvalidOperationException`. Invalid settings use `OptionsValidationException`, as Stage 1 specifies.

## Options and DI registration

The options classes are as approved in Stage 1, with no changes:

```csharp
public sealed class CacheMeOptions
{
    public string KeyPrefix { get; set; } = "";
    public TimeSpan? DefaultTtl { get; set; }
    public TimeSpan MaxTtl { get; set; } = TimeSpan.FromMinutes(5);
    public double RefreshThreshold { get; set; } = 0.75;
    public int MaxValueBytes { get; set; } = 1_048_576;
    public RedisOptions Redis { get; set; } = new();
    public WriteBehindOptions WriteBehind { get; set; } = new();
    public Dictionary<string, CachePolicyOptions> Policies { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class RedisOptions
{
    public string? ConnectionString { get; set; }
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromSeconds(1);
}

public sealed class WriteBehindOptions
{
    public TimeSpan MaxFlushDelay { get; set; } = TimeSpan.FromSeconds(5);
    public int MaxBatchSize { get; set; } = 100;
    public int MaxQueueLength { get; set; } = 100_000;
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public WriteBehindRetryOptions Retry { get; set; } = new();
    public DeadLetterOptions DeadLetter { get; set; } = new();
}

public sealed class WriteBehindRetryOptions
{
    public int MaxAttempts { get; set; } = 5;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
}

public sealed class DeadLetterOptions
{
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);
}

public sealed class CachePolicyOptions
{
    public TimeSpan? DefaultTtl { get; set; }
    public TimeSpan? MaxTtl { get; set; }
    public double? RefreshThreshold { get; set; }
}
```

The DI extension methods:

```csharp
public static class CacheMeServiceCollectionExtensions
{
    // Binds settings from configuration, then applies the builder.
    public static IServiceCollection AddCacheMe(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<CacheMeBuilder> configure);

    // Settings from code only, through CacheMeBuilder.Configure.
    public static IServiceCollection AddCacheMe(
        this IServiceCollection services,
        Action<CacheMeBuilder> configure);
}
```

Each `ForType<T>` registers `ICache<T>` as a singleton. Injecting `ICache<T>` for a type that was not registered fails when the service is resolved.

## Dependencies (seams)

| Interface | External concern | Visibility | Constructor used by tests and DI | Requirement |
|-----------|------------------|------------|----------------------------------|-------------|
| `ICacheStore` | The cache | public | `Cache<T>` internal constructor | FR-036, TD-009 |
| `IWriteBehindStore` | The queue and dead-letter store | public | `Cache<T>` and `WriteBehindProcessor` internal constructors | FR-020, FR-026, TD-009 |
| `ICacheSerializer` | Serialization | public | `Cache<T>` and `WriteBehindProcessor` internal constructors | FR-035, TD-009 |
| `ICacheLoader<T>`, `ICacheWriter<T>` | The database | public | Resolved through `IServiceScopeFactory` | FR-007, FR-017, TD-009 |
| `IDeadLetterHandler` | The application's alerting | public | Resolved through `IServiceScopeFactory` | FR-025, TD-009 |
| `IServiceScopeFactory` | A new DI scope for each use | public (Microsoft) | `Cache<T>` and `WriteBehindProcessor` internal constructors | TD-006 |
| `IOptions<CacheMeOptions>` | Settings | public (Microsoft) | `Cache<T>` and `WriteBehindProcessor` internal constructors | TD-004 |
| `ILogger<T>` | Logging | public (Microsoft) | `Cache<T>` and `WriteBehindProcessor` internal constructors | TD-008 |
| `TimeProvider` | The clock. It is an abstract class, not an interface, as Stage 0 decided (Q-065). Moq can mock it, and tests can use `FakeTimeProvider`. | public (.NET) | `Cache<T>` and `WriteBehindProcessor` internal constructors | TD-009 |
| `IBackgroundWork` | Runs refreshes off the request path, so tests can run them directly | internal | `Cache<T>` internal constructor | FR-008 |
| `ICacheMetrics` | Records metrics, so tests can check them | internal | `Cache<T>` and `WriteBehindProcessor` internal constructors | NFR-008 |
| `IWriteBehindStatus` | Tells whether the background service is running | internal | `Cache<T>` internal constructor | FR-020, FR-028 |

```csharp
internal interface IBackgroundWork { void Run(Func<CancellationToken, Task> work); }

internal interface ICacheMetrics
{
    void RecordHit(string valueType);
    void RecordMiss(string valueType);
    void RecordError(string valueType, CacheErrorKind kind);
}

internal interface IWriteBehindStatus { bool IsRunning { get; } }

internal sealed class CachePolicy<T> where T : class { /* The policy's strategies, loader and writer types, and settings key. */ }

// Internal constructor, used by AddCacheMe and the tests.
internal Cache(
    CachePolicy<T> policy,
    ICacheStore store,
    IWriteBehindStore? writeBehindStore,
    ICacheSerializer serializer,
    IServiceScopeFactory scopeFactory,
    IBackgroundWork backgroundWork,
    IWriteBehindStatus writeBehindStatus,
    ICacheMetrics metrics,
    IOptions<CacheMeOptions> options,
    TimeProvider timeProvider,
    ILogger<Cache<T>> logger);

// Processes the queue. Internal, but the tests call it directly, because it covers FR-022 to FR-028.
internal sealed class WriteBehindProcessor
{
    internal WriteBehindProcessor(
        IWriteBehindStore store,
        CachePolicyRegistry policies,
        ICacheSerializer serializer,
        IServiceScopeFactory scopeFactory,
        ICacheMetrics metrics,
        IOptions<CacheMeOptions> options,
        TimeProvider timeProvider,
        ILogger<WriteBehindProcessor> logger);

    // Claims and writes one batch. Returns how many writes were processed.
    internal Task<int> FlushOnceAsync(CancellationToken cancellationToken);

    // Flushes until the queue is empty or the shutdown timeout passes.
    internal Task FlushAllAsync(CancellationToken cancellationToken);
}

// Maps a QueuedWrite's ValueType to its value type and writer.
internal sealed class CachePolicyRegistry { }
```

**`InternalsVisibleTo` entries:** `CacheMe.Tests` and `DynamicProxyGenAssembly2`.

The Redis store and the hosted background service are internal. Stage 3's Redis container tests exercise the Redis store, and Stage 4 designs both.

## Client usage example

```csharp
// --- The domain's port. It has no reference to CacheMe. ---
public interface IProductRepository
{
    Task<Product?> GetAsync(int id, CancellationToken cancellationToken);
    Task SaveAsync(Product product, CancellationToken cancellationToken);
}

// --- Keys, shared by the repository and the loader. ---
internal static class ProductKeys
{
    public static string For(int id) => $"product:{id}";
    public static int ParseId(string key) => int.Parse(key.AsSpan("product:".Length));
}

// --- The outbound adapter. ---
public sealed class ProductLoader(ShopDbContext db) : ICacheLoader<Product>
{
    public Task<Product?> LoadAsync(string key, CancellationToken cancellationToken) =>
        db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == ProductKeys.ParseId(key), cancellationToken);
}

public sealed class ProductWriter(ShopDbContext db) : ICacheWriter<Product>
{
    public async Task WriteAsync(string key, Product value, CancellationToken cancellationToken)
    {
        db.Products.Update(value);
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ProductRepository(ICache<Product> cache) : IProductRepository
{
    public async Task<Product?> GetAsync(int id, CancellationToken cancellationToken)
    {
        var result = await cache.GetAsync(ProductKeys.For(id), cancellationToken);
        return result.Status switch
        {
            ReadStatus.Found => result.Value,
            ReadStatus.NotFound => null,
            _ => throw new ProductStoreException(result.Error!.Message, result.Error.Exception)
        };
    }

    public async Task SaveAsync(Product product, CancellationToken cancellationToken)
    {
        var result = await cache.WriteAsync(ProductKeys.For(product.Id), product, cancellationToken: cancellationToken);
        if (!result.Succeeded)
            throw new ProductStoreException(result.Error!.Message, result.Error.Exception);
    }
}

// --- Program.cs ---
builder.Services.AddDbContext<ShopDbContext>(/* your database */);

builder.Services.AddCacheMe(builder.Configuration.GetSection("CacheMe"), cache =>
{
    cache.UseRedis();
    cache.ForType<Product>(policy => policy
        .ReadThrough<ProductLoader>()
        .RefreshAhead()
        .WriteThrough<ProductWriter>());
});

builder.Services.AddScoped<IProductRepository, ProductRepository>();
```

`ProductStoreException` and `ShopDbContext` belong to the application.

## Coverage

| Requirement | API member(s) |
|-------------|---------------|
| FR-001 | `CacheMeBuilder.ForType`, `CachePolicyBuilder<T>` |
| FR-002 | `CachePolicyBuilder<T>` |
| FR-003 | `ICache<T>.GetAsync`, `ICache<T>.WriteAsync` (`InvalidOperationException`) |
| FR-004 | `ICache<T>.GetAsync`, `ReadResult<T>`, `ReadStatus` |
| FR-005 | `CachePolicyBuilder<T>.CacheAside`, `ICache<T>.GetAsync` |
| FR-006 | `ICache<T>.StoreInCacheAsync` |
| FR-007 | `CachePolicyBuilder<T>.ReadThrough`, `ICacheLoader<T>`, `ICache<T>.GetAsync` |
| FR-008 | `CachePolicyBuilder<T>.RefreshAhead`, `ICache<T>.GetAsync`, `CacheEntry` |
| FR-009 | `CacheMeOptions.RefreshThreshold`, `CachePolicyOptions.RefreshThreshold` |
| FR-010 | `ICache<T>.GetAsync`, `CacheEntry.HasQueuedWrite` |
| FR-011 | `ICache<T>.GetAsync`, `ICacheLoader<T>` |
| FR-012 | `ICache<T>.GetAsync`, `CacheErrorKind.StoreUnavailable` |
| FR-013 | `ICache<T>.GetAsync`, `CacheErrorKind.LoaderFailed` |
| FR-014 | `ICacheLoader<T>`, `ReadStatus.NotFound` |
| FR-015 | `ICache<T>.GetAsync` |
| FR-016 | `ICache<T>.GetAsync`, `ICacheSerializer.Deserialize` |
| FR-017 | `CachePolicyBuilder<T>.WriteThrough`, `ICacheWriter<T>`, `ICache<T>.WriteAsync` |
| FR-018 | `ICache<T>.WriteAsync`, `CacheErrorKind.WriterFailed` |
| FR-019 | `ICache<T>.WriteAsync` |
| FR-020 | `CachePolicyBuilder<T>.WriteBehind`, `ICache<T>.WriteAsync`, `IWriteBehindStore.TryEnqueueAsync` |
| FR-021 | `ICache<T>.WriteAsync`, `CacheErrorKind.StoreUnavailable` |
| FR-022 | `WriteBehindOptions.MaxFlushDelay`, `WriteBehindOptions.MaxBatchSize`, `IWriteBehindStore.ClaimAsync`, `WriteBehindProcessor.FlushOnceAsync` |
| FR-023 | `IWriteBehindStore.TryEnqueueAsync`, `IWriteBehindStore.CompleteAsync`, `QueuedWrite.Version` |
| FR-024 | `IWriteBehindStore.ClaimAsync` |
| FR-025 | `WriteBehindRetryOptions`, `IWriteBehindStore.RetryLaterAsync`, `IWriteBehindStore.MoveToDeadLetterAsync`, `IDeadLetterHandler`, `CacheMeBuilder.UseDeadLetterHandler` |
| FR-026 | `DeadLetterOptions.Retention`, `IWriteBehindStore.MoveToDeadLetterAsync`, `IDeadLetterStore`, `DeadLetterWrite` |
| FR-027 | `IWriteBehindStore.TryEnqueueAsync`, `IWriteBehindStore.CompleteAsync` |
| FR-028 | `WriteBehindOptions.ShutdownTimeout`, `WriteBehindProcessor.FlushAllAsync` |
| FR-029 | `WriteBehindOptions.MaxQueueLength`, `IWriteBehindStore.TryEnqueueAsync`, `CacheErrorKind.QueueFull` |
| FR-030 | `CachePolicyBuilder<T>.WriteAround`, `ICache<T>.WriteAsync` |
| FR-031 | `ICache<T>.WriteAsync` |
| FR-032 | `ICache<T>.RemoveAsync`, `IWriteBehindStore.CancelAsync` |
| FR-033 | `key` on all `ICache<T>` members, `CacheMeOptions.KeyPrefix` |
| FR-034 | `ttl` on `StoreInCacheAsync` and `WriteAsync`, `CacheMeOptions.DefaultTtl`, `CachePolicyOptions.DefaultTtl` |
| FR-035 | `ICacheSerializer`, `CacheMeBuilder.UseSerializer` |
| FR-036 | `CacheMeBuilder.UseRedis`, `CacheMeBuilder.UseStore`, `ICacheStore` |
| FR-037 | Exceptions on all `ICache<T>` members |
| FR-038 | `CacheMeOptions.MaxValueBytes`, `CacheErrorKind.ValueTooLarge` |
| NFR-002 | `CacheMeOptions.MaxTtl`, `CachePolicyOptions.MaxTtl` |
| NFR-003 | `RedisOptions.OperationTimeout` |
| NFR-004 | `CacheMeOptions.MaxValueBytes` |
| NFR-005 | All public services |
| NFR-006 | `IWriteBehindStore.ClaimAsync` |
| NFR-007 | `IWriteBehindStore`, `QueuedWrite` |
| NFR-008 | `CacheMeMetrics`, `IWriteBehindStore.GetQueueLengthAsync` |

NFR-001 is a performance target with no member of its own.

**FRs with no member:** none
**Members with no FR:** none

## Assumptions

These are design choices for you to correct if they are wrong:

- **Keys stay strings.** The configuration document asked this stage to check for a typed ID. FR-033 decided that the caller passes a string, and a background refresh only has the key to go on, so a typed ID would mean changing Stage 0. The usage example keeps the parsing in one small `ProductKeys` helper instead. To change this, run `/lib-designer:analysis` with the change.
- **`T` must be a class** (`where T : class`). This narrows Q-012 in Stage 0, because a loader's null result has to mean not found, which does not work for a value type such as `int`. To cache one, wrap it in a class or record.
- **`StoreInCacheAsync` works only with cache-aside.** The other read strategies populate the cache themselves, so using it there would be a mistake.
- **An oversized value is a result, not an exception.** Its size is known only after serializing, and it depends on runtime data, so it returns `ValueTooLarge`.
- **Added registration rule.** A policy using write-behind with a store that is not an `IWriteBehindStore` fails at registration. Stage 1 did not list this rule, but it follows from splitting the store interfaces.
- **Cancellation throws.** A cancelled token raises `OperationCanceledException`, as usual in .NET, rather than returning a result.
- **The default serializer is internal.** It is replaced through `UseSerializer`.

## Open Questions

None.

## Decisions

### Q-001 Generic or single cache interface
**Question:** Is the cache interface generic, or a single interface with generic methods?
**Answer:** select suggested (answered in chat)
**Interpretation:** `ICache<T>`, with one injected service per value type.

### Q-002 How write-behind calls the writer
**Question:** How does a write-behind flush call the writer?
**Answer:** select suggested (answered in chat)
**Interpretation:** One `ICacheWriter<T>.WriteAsync` call per queued write.

### Q-003 Reading dead-letter writes
**Question:** How does an application read dead-letter writes?
**Answer:** select suggested (answered in chat)
**Interpretation:** Add `IDeadLetterStore`, with list and delete, for the application to inject.
