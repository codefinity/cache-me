# CacheMe

CacheMe gives a .NET application the common read and write caching strategies, on Redis or another store. You choose a strategy for each value type at startup, and your repositories call one small interface.

It targets .NET 10 and needs Microsoft dependency injection. Write-behind also needs the .NET generic host, which ASP.NET Core and worker services already use.

## Quick start

**1. Write a loader and a writer.** They read and save one value, and contain only database code. CacheMe calls them when the strategy needs the database.

```csharp
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
```

**2. Register CacheMe.**

```csharp
builder.Services.AddCacheMe(builder.Configuration.GetSection("CacheMe"), cache =>
{
    cache.UseRedis();
    cache.ForType<Product>(policy => policy
        .ReadThrough<ProductLoader>()
        .WriteThrough<ProductWriter>());
});
```

```json
{
  "CacheMe": {
    "KeyPrefix": "shop:",
    "DefaultTtl": "00:05:00",
    "Redis": { "ConnectionString": "localhost:6379" }
  }
}
```

**3. Inject the cache into your repository.**

```csharp
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
```

The repository never names a strategy, so changing one means changing only your registration code. `ProductKeys` is your own small helper that builds a key from an ID and parses it back.

## Strategies

Each value type has one policy, with a read strategy, a write strategy or both. Any read strategy can be combined with any write strategy.

| Strategy | Register with | What happens |
|----------|---------------|--------------|
| Cache-aside | `CacheAside()` | A miss returns not found. Your repository loads the value, then calls `StoreInCacheAsync`. |
| Read-through | `ReadThrough<TLoader>()` | On a miss, CacheMe calls the loader, stores the result and returns it. |
| Refresh-ahead | `RefreshAhead()`, after `ReadThrough` | A read of an entry past the threshold returns the current value and reloads it in the background. |
| Write-through | `WriteThrough<TWriter>()` | CacheMe calls the writer, then updates the cache. |
| Write-behind | `WriteBehind<TWriter>()` | CacheMe stores the value and queues the database write, which happens later in the background. |
| Write-around | `WriteAround<TWriter>()` | CacheMe calls the writer, then removes the cache entry. The value is not cached. |

## Handling results

Runtime failures come back as results, not exceptions. A read returns `Found`, `NotFound` or `Failed`. A write returns `Succeeded`, or an `Error` with one of these kinds:

| `CacheErrorKind` | Meaning |
|------------------|---------|
| `StoreUnavailable` | The cache store failed or timed out. |
| `LoaderFailed` | Your loader threw. |
| `WriterFailed` | Your writer threw. |
| `QueueFull` | The write-behind queue is at `MaxQueueLength`. |
| `ValueTooLarge` | The serialized value is over `MaxValueBytes`. |
| `SerializationFailed` | The serializer threw on a write. |

Exceptions are for mistakes in your code or configuration: invalid keys, null values, out-of-range TTLs, or calling an operation the policy doesn't define. Invalid settings stop the application at startup.

If the cache is down, read-through falls back to your loader, so your database takes all the read traffic during the outage. Cache-aside returns `StoreUnavailable`, so your repository can decide what to do.

## Settings

All settings are under the `CacheMe` section. Code in `cache.Configure(...)` runs after JSON, so it wins. An unknown key stops the application at startup, so typos can't silently fall back to a default.

| Key | Default | Meaning |
|-----|---------|---------|
| `KeyPrefix` | Required | Added before every key. Change it (for example, to `shop:v2:`) to version keys after a type changes. |
| `DefaultTtl` | Required | TTL when a write gives none. |
| `MaxTtl` | `00:05:00` | Longest TTL allowed. A longer default stops startup, and a longer per-write TTL throws. |
| `RefreshThreshold` | `0.75` | Share of TTL after which a read starts a refresh. |
| `MaxValueBytes` | `1048576` | Largest serialized value a write accepts. Not applied to write-around. |
| `Redis:ConnectionString` | — | The connection CacheMe creates, with one endpoint only. |
| `Redis:OperationTimeout` | `00:00:01` | Time limit for each Redis operation. |
| `WriteBehind:MaxFlushDelay` | `00:00:05` | Longest a write waits before a flush. |
| `WriteBehind:MaxBatchSize` | `100` | Most writes claimed in one flush. |
| `WriteBehind:MaxQueueLength` | `100000` | Most queued writes in total, across all instances. |
| `WriteBehind:ShutdownTimeout` | `00:00:10` | Longest the flush runs at shutdown. Keep it below the host's shutdown timeout. |
| `WriteBehind:Retry:MaxAttempts` | `5` | Total tries for a queued write. |
| `WriteBehind:Retry:InitialDelay` | `00:00:01` | Wait before the second try, which then doubles each time. |
| `WriteBehind:Retry:MaxDelay` | `00:00:30` | Longest wait between tries. |
| `WriteBehind:DeadLetter:Retention` | `7.00:00:00` | How long failed writes are kept. |
| `Policies:<TypeName>:DefaultTtl` | `DefaultTtl` | Default TTL for one type. |
| `Policies:<TypeName>:MaxTtl` | `MaxTtl` | TTL limit for one type. |
| `Policies:<TypeName>:RefreshThreshold` | `RefreshThreshold` | Refresh threshold for one type. |

Don't commit a connection string that contains a password. Supply it from user secrets, your secret store, or the `CacheMe__Redis__ConnectionString` environment variable.

## Behaviour you need to know about

**Writes run outside your database transaction.** A writer gets its own DI scope and its own `DbContext`. If a use case must save several things in one transaction, don't send those writes through a writer.

**A write-behind write can run more than once.** If an instance crashes after your writer finishes but before the write is marked complete, another instance repeats it. Make writers safe to repeat. Saving a whole value is usually fine, but be careful with writers that insert rows or add to a total.

**Write-behind can lose data if Redis loses data.** Queued writes survive application restarts, but they live in Redis. Turn on AOF persistence, and don't use an `allkeys-*` eviction policy. CacheMe logs a warning at startup if it can read these settings and they are unsafe.

**Write-behind keeps order per key only.** Rows with foreign keys between them can arrive out of order, so don't use write-behind for them.

**Failed writes stay in the dead-letter store.** After its retries run out, a write moves there for `Retention`. CacheMe does not replay it. Set a handler with `UseDeadLetterHandler<THandler>()` to be notified, or inject `IDeadLetterStore` to list and delete failed writes.

**A slow reader can store an old value.** A read can load a row, a write can then finish, and the read can then cache the old row. It stays until its TTL ends, which `MaxTtl` limits. Changes made directly to the database are also unseen until the TTL ends.

**Don't cache sensitive data without encryption.** Anyone with access to Redis can read cached values. Use `UseSerializer<TSerializer>()` with an encrypting serializer if you need to store it.

**Value types must be classes.** To cache an `int` or other struct, wrap it in a class or record.

**Cached data can outlive a type change.** An entry that can't be read counts as a miss and logs a warning. A queued write that can't be read goes to the dead-letter store.

## Redis

CacheMe supports a single Redis endpoint, not Sentinel or Cluster. To share an existing connection, use `cache.UseRedis(provider => connection)`. It reserves keys that start with `<KeyPrefix>__cacheme:`, so don't use that for your own keys.

To use another store, implement `ICacheStore`, plus `IWriteBehindStore` if you need write-behind, and register it with `cache.UseStore<TStore>()`.

## Metrics

The meter is named `CacheMe`. Its instruments are `cacheme.hits`, `cacheme.misses`, `cacheme.errors` and `cacheme.write_behind.queue_depth`. The names are available as constants on `CacheMeMetrics`.
