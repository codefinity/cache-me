# Library Configuration

| Field       | Value                         |
|-------------|-------------------------------|
| Stage       | 1 – Configuration             |
| Version     | v2                            |
| Date        | 2026-11-01                    |
| Status      | Awaiting answers              |
| Approved on | —                             |
| Based on    | analysis-v3.md (Approved)     |
| Supersedes  | v1                            |

## Changelog
- v2: Added a configuration example for each of the six strategies and for the combinations in the root `README.md`, as you asked. Added an assumption that `MaxValueBytes` does not apply to write-around.

## Summary of the configuration approach

Configuration has two parts (TD-004):

- **Settings** are values such as TTLs, timeouts and limits. They live in the `CacheMe` section of `appsettings.json`, bind to `IOptions<CacheMeOptions>`, and can be overridden in code. Code runs after JSON, so code wins.
- **Types** are registered in code, because JSON cannot name them safely: strategies with their loaders and writers (FR-001), the cache store (FR-036), the serializer (FR-035) and the dead-letter handler (FR-025).

Each value type has one policy (FR-001). Its settings can be overridden under `CacheMe:Policies:<TypeName>`. The sections below assume the suggested answers to Q-001 and Q-002.

Options are read once at startup, and a change takes effect after a restart. The default serializer uses `System.Text.Json` with `JsonSerializerDefaults.Web`; to change this, replace the serializer. The clock is the `TimeProvider` registered in DI, or `TimeProvider.System` if there is none (TD-009).

The builder methods and interfaces below show the shape of registration. Stage 2 finalizes their exact names and signatures.

## Options reference

**General**

| Key | C# property | Type | Required | Default | Validation / allowed values | Description | Requirement |
|-----|-------------|------|----------|---------|-----------------------------|-------------|-------------|
| `CacheMe:KeyPrefix` | `KeyPrefix` | `string` | yes | — | Not empty, no whitespace, at most 128 characters | Added as is before every key. Change it (for example, `shop:v2:`) to version keys after a type changes. | FR-033, FR-016 |
| `CacheMe:DefaultTtl` | `DefaultTtl` | `TimeSpan` | yes | — | Greater than zero; at most the effective `MaxTtl` | TTL for an entry when a write gives none. | FR-034 |
| `CacheMe:MaxTtl` | `MaxTtl` | `TimeSpan` | no | `00:05:00` | Greater than zero | Longest TTL allowed, which limits staleness from outside database changes (Q-002). | NFR-002 |
| `CacheMe:RefreshThreshold` | `RefreshThreshold` | `double` | no | `0.75` | Greater than 0 and less than 1 | Share of TTL after which a read starts a background reload. | FR-008, FR-009 |
| `CacheMe:MaxValueBytes` | `MaxValueBytes` | `int` | no | `1048576` | From 1 to 536870912 (the Redis limit) | Largest serialized value a write accepts. | FR-038, NFR-004 |

**Redis**

| Key | C# property | Type | Required | Default | Validation / allowed values | Description | Requirement |
|-----|-------------|------|----------|---------|-----------------------------|-------------|-------------|
| `CacheMe:Redis:ConnectionString` | `Redis.ConnectionString` | `string?` | When `UseRedis()` has no connection supplied | — | Valid `StackExchange.Redis` configuration; one endpoint only | Connection the library creates. | FR-036, TD-009, TD-011 |
| `CacheMe:Redis:OperationTimeout` | `Redis.OperationTimeout` | `TimeSpan` | no | `00:00:01` | Greater than zero | Time limit for each Redis operation. | NFR-003 |

**Write-behind**

| Key | C# property | Type | Required | Default | Validation / allowed values | Description | Requirement |
|-----|-------------|------|----------|---------|-----------------------------|-------------|-------------|
| `CacheMe:WriteBehind:MaxFlushDelay` | `WriteBehind.MaxFlushDelay` | `TimeSpan` | no | `00:00:05` | Greater than zero | Longest a write waits before a flush. | FR-022 |
| `CacheMe:WriteBehind:MaxBatchSize` | `WriteBehind.MaxBatchSize` | `int` | no | `100` | At least 1 | Most writes in one flush. | FR-022 |
| `CacheMe:WriteBehind:MaxQueueLength` | `WriteBehind.MaxQueueLength` | `int` | no | `100000` | At least `MaxBatchSize` | Most queued writes in total, across all instances. New writes return an error at this limit. At 1 KB per value, a full queue takes about 100 MB in Redis. | FR-029 |
| `CacheMe:WriteBehind:ShutdownTimeout` | `WriteBehind.ShutdownTimeout` | `TimeSpan` | no | `00:00:10` | Greater than zero | Longest a flush runs at shutdown. Keep it below the host's shutdown timeout, which defaults to 30 seconds. | FR-028 |
| `CacheMe:WriteBehind:Retry:MaxAttempts` | `WriteBehind.Retry.MaxAttempts` | `int` | no | `5` | At least 1 | Total tries for a queued write, including the first. | FR-025 |
| `CacheMe:WriteBehind:Retry:InitialDelay` | `WriteBehind.Retry.InitialDelay` | `TimeSpan` | no | `00:00:01` | Greater than zero | Wait before the second try. The wait then doubles each time (1, 2, then 4 seconds, and so on). | FR-025 |
| `CacheMe:WriteBehind:Retry:MaxDelay` | `WriteBehind.Retry.MaxDelay` | `TimeSpan` | no | `00:00:30` | At least `InitialDelay` | Longest wait between tries. | FR-025 |
| `CacheMe:WriteBehind:DeadLetter:Retention` | `WriteBehind.DeadLetter.Retention` | `TimeSpan` | no | `7.00:00:00` | Greater than zero | How long failed writes stay in the dead-letter store. | FR-026 |

**Per value type** (`<TypeName>` is the type's short name, for example `Product`)

| Key | C# property | Type | Required | Default | Validation / allowed values | Description | Requirement |
|-----|-------------|------|----------|---------|-----------------------------|-------------|-------------|
| `CacheMe:Policies:<TypeName>:DefaultTtl` | `Policies[name].DefaultTtl` | `TimeSpan?` | no | `DefaultTtl` | Greater than zero; at most the effective `MaxTtl` | Default TTL for this type. | FR-034 |
| `CacheMe:Policies:<TypeName>:MaxTtl` | `Policies[name].MaxTtl` | `TimeSpan?` | no | `MaxTtl` | Greater than zero | Staleness limit for this type. | NFR-002 |
| `CacheMe:Policies:<TypeName>:RefreshThreshold` | `Policies[name].RefreshThreshold` | `double?` | no | `RefreshThreshold` | Greater than 0 and less than 1; only for a type using refresh-ahead | Refresh threshold for this type. | FR-009 |

## Example `appsettings.json`

The connection string has no password here. See [Secrets and environment-specific overrides](#secrets-and-environment-specific-overrides).

```json
{
  "CacheMe": {
    "KeyPrefix": "shop:",
    "DefaultTtl": "00:05:00",
    "MaxTtl": "00:05:00",
    "RefreshThreshold": 0.75,
    "MaxValueBytes": 1048576,
    "Redis": {
      "ConnectionString": "localhost:6379",
      "OperationTimeout": "00:00:01"
    },
    "WriteBehind": {
      "MaxFlushDelay": "00:00:05",
      "MaxBatchSize": 100,
      "MaxQueueLength": 100000,
      "ShutdownTimeout": "00:00:10",
      "Retry": {
        "MaxAttempts": 5,
        "InitialDelay": "00:00:01",
        "MaxDelay": "00:00:30"
      },
      "DeadLetter": {
        "Retention": "7.00:00:00"
      }
    },
    "Policies": {
      "Product": {
        "DefaultTtl": "00:02:00",
        "RefreshThreshold": 0.8
      },
      "PageView": {
        "MaxTtl": "00:30:00",
        "DefaultTtl": "00:30:00"
      }
    }
  }
}
```

## Client registration

**Typical setup:** settings from JSON, with types registered in code.

```csharp
services.AddCacheMe(configuration.GetSection("CacheMe"), cache =>
{
    cache.UseRedis();

    // Read-through with refresh-ahead, and write-through.
    cache.ForType<Product>(policy => policy
        .ReadThrough<ProductLoader>()
        .RefreshAhead()
        .WriteThrough<ProductWriter>());

    // Write-only.
    cache.ForType<PageView>(policy => policy
        .WriteBehind<PageViewWriter>());

    // Read-only, where the repository handles the miss.
    cache.ForType<Country>(policy => policy
        .CacheAside());

    // Optional: notified when a queued write reaches the dead-letter store.
    cache.UseDeadLetterHandler<AlertingDeadLetterHandler>();
});
```

**Overriding settings in code:** this runs after JSON.

```csharp
services.AddCacheMe(configuration.GetSection("CacheMe"), cache =>
{
    cache.UseRedis();
    cache.Configure(options => options.KeyPrefix = $"shop:{environment.EnvironmentName}:");
    cache.ForType<Product>(policy => policy.ReadThrough<ProductLoader>());
});
```

**Without JSON:**

```csharp
services.AddCacheMe(cache =>
{
    cache.Configure(options =>
    {
        options.KeyPrefix = "shop:";
        options.DefaultTtl = TimeSpan.FromMinutes(5);
    });
    cache.UseRedis(connectionString);
    cache.ForType<Product>(policy => policy.ReadThrough<ProductLoader>());
});
```

**Other connections and stores:**

```csharp
// Use the application's existing connection.
cache.UseRedis(serviceProvider => serviceProvider.GetRequiredService<IConnectionMultiplexer>());

// Use another store through the public store interface.
cache.UseStore<MyCacheStore>();

// Replace the serializer.
cache.UseSerializer<MyEncryptingSerializer>();
```

**Two types with the same short name:** pass a settings key to tell them apart.

```csharp
cache.ForType<Catalog.Product>("CatalogProduct", policy => policy.ReadThrough<CatalogProductLoader>());
```

`ForType` registers loaders and writers as scoped services, unless they are already registered. Each use gets a new DI scope (TD-006).

## Configuration for each strategy

The examples below show only what each strategy adds. Every application also needs this base configuration:

```json
{
  "CacheMe": {
    "KeyPrefix": "shop:",
    "DefaultTtl": "00:05:00",
    "Redis": {
      "ConnectionString": "localhost:6379"
    }
  }
}
```

```csharp
services.AddCacheMe(configuration.GetSection("CacheMe"), cache =>
{
    cache.UseRedis();
    // Add the ForType calls from the examples here.
});
```

### What each strategy needs

| Strategy | Register in code | Settings it uses, besides the base | Requirement |
|----------|------------------|-------------------------------------|-------------|
| Cache-aside | Nothing extra | `MaxTtl`, `MaxValueBytes` | FR-005, FR-006 |
| Read-through | A loader | `MaxTtl`, `MaxValueBytes` | FR-007 |
| Refresh-ahead | A loader, through read-through | The above, plus `RefreshThreshold` | FR-008, FR-009 |
| Write-through | A writer | `MaxTtl`, `MaxValueBytes` | FR-017 |
| Write-behind | A writer; optionally a dead-letter handler | The above, plus everything under `WriteBehind` | FR-020, FR-022 |
| Write-around | A writer | None, because the value is not stored in the cache | FR-030 |

A policy with only a read strategy, or only a write strategy, cannot do the other operation. Calling it is a programmer error (FR-003). The combinations further down show how to set both.

### Cache-aside

**Example use:** country reference data that rarely changes. The repository loads the data itself when an entry is missing. This example allows a one-hour TTL, so outside changes can stay unseen for up to an hour.

```json
"Policies": {
  "Country": {
    "MaxTtl": "01:00:00",
    "DefaultTtl": "01:00:00"
  }
}
```

```csharp
cache.ForType<Country>(policy => policy.CacheAside());
```

**Result:** a read checks only the cache. On a miss, it returns not found, the repository loads the country, and it stores it with the cache-only operation.

### Read-through

**Example use:** products, where the repository should not handle misses.

```json
"Policies": {
  "Product": {
    "DefaultTtl": "00:02:00"
  }
}
```

```csharp
cache.ForType<Product>(policy => policy.ReadThrough<ProductLoader>());
```

**Result:** on a miss, the library calls `ProductLoader`, stores the result for 2 minutes and returns it.

### Refresh-ahead

**Example use:** exchange rates that are read constantly and must not cause slow reloads. Refresh-ahead is an add-on to read-through (FR-008).

```json
"Policies": {
  "ExchangeRate": {
    "DefaultTtl": "00:01:00",
    "RefreshThreshold": 0.8
  }
}
```

```csharp
cache.ForType<ExchangeRate>(policy => policy
    .ReadThrough<ExchangeRateLoader>()
    .RefreshAhead());
```

**Result:** a read of an entry older than 48 seconds (80% of 1 minute) returns the current value at once and starts a background reload. Without `RefreshThreshold` here, the general default of 75% applies.

### Write-through

**Example use:** account settings, where a read straight after a write must see the new value.

```json
"Policies": {
  "AccountSettings": {
    "DefaultTtl": "00:05:00"
  }
}
```

```csharp
cache.ForType<AccountSettings>(policy => policy.WriteThrough<AccountSettingsWriter>());
```

**Result:** a write calls `AccountSettingsWriter`, then stores the value in the cache for 5 minutes. It reports success once both have the value.

### Write-behind

**Example use:** page-view counts, which are written often and can tolerate a short delay in reaching the database. Settings under `WriteBehind` apply to every type that uses write-behind. Only the TTL is per type.

```json
"WriteBehind": {
  "MaxFlushDelay": "00:00:02",
  "MaxBatchSize": 200,
  "MaxQueueLength": 50000,
  "ShutdownTimeout": "00:00:15",
  "Retry": {
    "MaxAttempts": 3,
    "InitialDelay": "00:00:02",
    "MaxDelay": "00:00:10"
  },
  "DeadLetter": {
    "Retention": "3.00:00:00"
  }
},
"Policies": {
  "PageView": {
    "MaxTtl": "00:30:00",
    "DefaultTtl": "00:30:00"
  }
}
```

```csharp
cache.ForType<PageView>(policy => policy.WriteBehind<PageViewWriter>());
cache.UseDeadLetterHandler<AlertingDeadLetterHandler>();
```

**Result:** a write stores the value in the cache, queues the database write in Redis and reports success. Queued writes are flushed every 2 seconds, or sooner once 200 are waiting. A write that fails 3 times moves to the dead-letter store for 3 days, and the handler is notified. An entry with a queued write does not expire until that write completes (FR-027).

### Write-around

**Example use:** audit events, which are written once and rarely read. The value never goes to the cache, so there are no type-specific settings.

```csharp
cache.ForType<AuditEvent>(policy => policy.WriteAround<AuditEventWriter>());
```

**Result:** a write calls `AuditEventWriter`, then removes any cache entry for the key. It reports success once the database has the value.

### Combining a read and a write strategy

One policy can set one read strategy and one write strategy (FR-002). These are the combinations listed in the root `README.md`.

```csharp
// Read-through + write-through: reads and writes both stay consistent with the database.
cache.ForType<Customer>(policy => policy
    .ReadThrough<CustomerLoader>()
    .WriteThrough<CustomerWriter>());

// Cache-aside + write-around: the repository handles misses, and writes skip the cache.
cache.ForType<Article>(policy => policy
    .CacheAside()
    .WriteAround<ArticleWriter>());

// Read-through + write-behind: fastest reads and writes, with a possible loss window.
cache.ForType<CartItem>(policy => policy
    .ReadThrough<CartItemLoader>()
    .WriteBehind<CartItemWriter>());

// Refresh-ahead with a write strategy: hot keys stay warm.
// A refresh is skipped while the key has a queued write (FR-010).
cache.ForType<Leaderboard>(policy => policy
    .ReadThrough<LeaderboardLoader>()
    .RefreshAhead()
    .WriteBehind<LeaderboardWriter>());
```

The README says refresh-ahead can sit on top of any read strategy. Stage 0 narrowed this to read-through only (FR-008), so `CacheAside().RefreshAhead()` is rejected at registration, because cache-aside has no loader to refresh with.

## Options classes

Shown here only; the code is created in Stage 2.

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

## Validation

Settings are validated with `IValidateOptions<CacheMeOptions>` and `ValidateOnStart()`, so a bad setting stops the application when the host starts. All failures are reported together. Messages name the key but never include the connection string, so secrets stay out of logs (TD-011).

**Stops the application**

| Rule | When it runs | On invalid configuration | Requirement |
|------|--------------|--------------------------|-------------|
| `KeyPrefix` is set and meets its rules | Startup | `OptionsValidationException`: `CacheMe:KeyPrefix is required and must have no whitespace and at most 128 characters.` | FR-033 |
| `DefaultTtl` is set and greater than zero | Startup | `OptionsValidationException`: `CacheMe:DefaultTtl is required and must be greater than zero.` | FR-034 |
| Each effective default TTL is at most its effective `MaxTtl` | Startup | `OptionsValidationException`: `CacheMe:Policies:Product:DefaultTtl (00:10:00) must not exceed MaxTtl (00:05:00).` | FR-034, NFR-002 |
| Each threshold, size, timeout, delay, count and retention is in the range in the options reference | Startup | `OptionsValidationException`: `CacheMe:<key> must be <rule>.` | FR-009, FR-022, FR-025, FR-026, FR-028, FR-029, FR-038, NFR-003 |
| A key under `CacheMe` matches a known setting, so that typos do not silently fall back to defaults | Startup | `InvalidOperationException`, listing the unknown key | TD-004 |
| A key under `CacheMe:Policies` matches a registered value type | Startup | `OptionsValidationException`: `CacheMe:Policies:Prodct does not match a registered value type.` | FR-001 |
| `RefreshThreshold` for a type appears only if that type uses refresh-ahead | Startup | `OptionsValidationException`: `CacheMe:Policies:Country:RefreshThreshold is set, but Country does not use refresh-ahead.` | FR-008 |
| `UseRedis()` with no connection supplied has a connection string | Startup | `OptionsValidationException`: `CacheMe:Redis:ConnectionString is required.` | FR-036 |
| The connection string can be parsed and has one endpoint | Startup | `OptionsValidationException`: `CacheMe:Redis:ConnectionString is invalid or lists more than one endpoint.` | TD-009 |
| Exactly one store is registered | Registration | `InvalidOperationException`: `Register exactly one cache store: UseRedis or UseStore.` | FR-036 |
| Each value type is registered once, and each settings key is used once | Registration | `InvalidOperationException`: `Product is already registered.` or `The settings key Product is already used. Pass a settings key to ForType.` | FR-001 |
| A policy sets at least one strategy, at most one read strategy and at most one write strategy | Registration | `InvalidOperationException`: `Product must set one read strategy, one write strategy or both.` | FR-001, FR-002 |
| Refresh-ahead is used with read-through | Registration | `InvalidOperationException`: `Product uses RefreshAhead, which requires ReadThrough.` | FR-008 |

**Reported later**

| Rule | When it runs | On invalid configuration | Requirement |
|------|--------------|--------------------------|-------------|
| The write-behind background service is running | First write-behind write | `InvalidOperationException`: `Write-behind requires the .NET generic host, because its queue is processed by a hosted service.` | FR-020, FR-028 |
| Redis can be reached | Startup | Logs a warning and does not stop the application, so reads can still fall back to the loader | FR-012 |
| Redis persistence and eviction are safe for the queue, when a type uses write-behind and the settings can be read | Startup | Logs a warning and does not stop the application | NFR-007 |

## Secrets and environment-specific overrides

The Redis connection string can hold a password, so it should not be committed with one (TD-011). Standard .NET configuration sources apply, and each can supply just this one value:

- **Local development:** .NET user secrets, or a local Redis with no password.
- **Environment variable:** `CacheMe__Redis__ConnectionString`.
- **Production:** your secret store, such as Azure Key Vault, through its configuration provider.

For TLS, add `ssl=true` to the connection string. TLS is supported but not required (Q-068 in Stage 0).

Other settings can vary by environment in `appsettings.<Environment>.json`. For example, give each environment its own `KeyPrefix` if they share a Redis.

## Assumptions

These are design choices for you to correct if they are wrong:

- Applications using write-behind run on the .NET generic host (ASP.NET Core or a worker service), because Stage 0 requires a flush at shutdown (FR-028). Other strategies do not need it, although without a host validation runs when options are first used rather than at startup.
- Settings do not need to change while the application is running.
- `KeyPrefix` has no default. Silently sharing one prefix would let applications on the same Redis overwrite each other's keys.
- The numeric defaults for the queue limit, retries, shutdown timeout and dead-letter retention are starting points, proposed here as Stage 0 deferred them. They are not based on measurements.
- `MaxValueBytes` does not apply to write-around, because the value never goes to Redis. FR-038 reads as covering all writes, but its purpose (Q-067 in Stage 0) is to protect Redis.
- A supplied `IConnectionMultiplexer` keeps its own connection settings. The single-endpoint check applies only to a connection string, and the library still applies `OperationTimeout`.

## Open Questions

### Q-001 [Important] Where strategies are chosen
**Why it matters:** Stage 0 set strategies at registration, but not whether in code or JSON. In JSON, a strategy could change without a rebuild, but a mistake such as naming a strategy with no writer would only show up at startup. In code, the compiler catches those mistakes, but a change needs a new build.
**Options:** a) Code only; JSON holds settings only  b) Strategy names in JSON, with loaders and writers registered in code  c) Code, with a JSON override
**Suggested:** a — changing a strategy changes consistency behaviour, which deserves a code review and a deployment.
**Answer:**
>

### Q-002 [Important] How the staleness limit is enforced
**Why it matters:** Stage 0 set a 5-minute limit, adjustable per type (NFR-002), but also allows a TTL override on each write (FR-034). Nothing yet stops a write from using a longer TTL. This design adds a `MaxTtl` setting to enforce the limit. Entries with a queued write can still outlive it (FR-027).
**Options:** a) Enforce it with `MaxTtl`: a longer default TTL stops startup, and a longer per-write TTL is a programmer error (FR-037)  b) Guidance only, with no `MaxTtl`  c) Enforce it, but clamp a longer per-write TTL to the limit and log a warning
**Suggested:** a — the limit only holds if it is enforced, and an error shows the mistake immediately.
**Answer:**
>

## Decisions

None yet.
