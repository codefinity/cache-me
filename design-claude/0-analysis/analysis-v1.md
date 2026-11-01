# Library Analysis

| Field       | Value                 |
|-------------|-----------------------|
| Stage       | 0 – Analysis          |
| Version     | v1                    |
| Date        | 2026-11-01            |
| Status      | Awaiting answers      |
| Approved on | —                     |
| Based on    | lib-requirements.md   |
| Supersedes  | —                     |

## Changelog
- v1: First version. Restates the requirements and raises the questions needed to write the Library Design Brief.

## Requirements as understood

The library lets an application cache values in Redis, or another caching database, without writing the caching logic itself. It implements the read and write strategies described in the root `README.md`:

- **Read:** cache-aside, read-through, refresh-ahead.
- **Write:** write-through, write-behind, write-around.

The caller is the repository layer (a driven adapter) of an application built with hexagonal architecture. On a write, the caller passes a value and a strategy, and gets back success or failure. On a read, the caller identifies what to read and passes a strategy, and gets back success or failure.

Failure behaviour was left for this stage to raise, with options. These parts of the requirements are not filled in, so they are raised as questions below: example usage (section 4), quality targets (section 5), external dependencies (section 6), out of scope (section 8), constraints (section 9) and open doubts (section 10). All of section 7 is marked Undecided.

The requirements point to "the diagram" for concrete examples. The diagrams show the flow between Application, Cache and Database but contain no concrete values.

**The main gaps these questions address:**

1. The requirements describe only a cache, but four of the six strategies have the cache layer read or write the database (see the README's comparison table). Section 6 lists no database, and nothing says how the library reaches one.
2. "Success if the value is cached" cannot hold for every strategy. Write-around does not cache the value, and write-behind reports success before the database has it.
3. "Failure if the value is not received" treats a normal cache miss the same as an outage.
4. Redis cannot call your database. Read-through, refresh-ahead, write-through and write-behind must therefore all run inside this library, including background work that outlives the request that started it.

## Assumptions

- The full set of strategies is the six in the root `README.md`, because the requirements refer to that list without narrowing it.
- There are no existing callers. The repository has no `.sln`, `.slnx`, `.csproj` or `.cs` files, and none in its git history.
- `src/CacheMe` and `tests/CacheMe.Tests` contain only ignored build output. Their folder names are used as suggestions below, not treated as decisions.

## Open Questions

**A. Scope and shape**

### Q-001 [Blocking] Difference between cache-aside and read-through
**Why it matters:** the README separates them by who owns the logic (the application or the cache). Once a library does the work and calls your loader, both behave the same for the caller: check the cache, load on a miss, store, return.
**Options:** a) Cache-aside exposes plain get and set, and the caller handles the miss; read-through takes a loader and handles the miss itself  b) One behaviour under two names  c) Drop cache-aside as a separate strategy
**Suggested:** a — it keeps the README's distinction and gives each strategy a reason to exist.
**Answer:**
>

### Q-002 [Blocking] How the library reaches the database
**Why it matters:** read-through, refresh-ahead, write-through and write-behind all need database access. Refresh-ahead and write-behind run after the request has ended, so a per-call delegate that captures a scoped `DbContext` would use a disposed object. A delegate also cannot be stored in a durable queue (Q-036).
**Options:** a) Registered loader and writer types per value type, resolved in a new DI scope for each use  b) Per-call delegates  c) Both
**Suggested:** a — it is the only option that is safe for background work and works with a durable queue.
**Answer:**
>

### Q-003 [Blocking] Where the strategy is chosen
**Why it matters:** the requirements say the caller passes the strategy on each call. That lets two callers treat the same key differently and puts caching policy in repository code. For example, a write-behind followed by a cache-aside read from another caller could see inconsistent results.
**Options:** a) Per call, as written in the requirements  b) At registration, per value type or named policy  c) Registered default with a per-call override
**Suggested:** b — one key gets one consistent behaviour, and a policy change does not touch repositories. This changes what the requirements say, so it is your call.
**Answer:**
>

### Q-004 [Important] Refresh-ahead as a strategy or an add-on
**Why it matters:** the README lists it as a read strategy, but also as "on top of any read strategy". A key that has never been loaded still needs some behaviour on a miss.
**Options:** a) Add-on to read-through, which handles the miss  b) Standalone, where a miss returns not found  c) Add-on to any read strategy
**Suggested:** a — it already has a loader, which refresh-ahead needs anyway.
**Answer:**
>

### Q-005 [Important] Allowed read and write combinations
**Why it matters:** the README lists a few combinations, but not whether others are allowed. Validation and the number of test cases depend on this.
**Options:** a) Any read strategy with any write strategy  b) Only the combinations in the README; others are rejected at startup
**Suggested:** a — none of the pairs is unsafe on its own, and the trade-offs can be documented.
**Answer:**
>

### Q-006 [Blocking] One application instance or many
**Why it matters:** with many instances, the same key can be loaded or refreshed by several at once, and a write-behind queue held in memory is invisible to the others.
**Options:** a) Always one instance  b) Many instances behind a load balancer
**Suggested:** b — designing for one instance would be expensive to undo later.
**Answer:**
>

### Q-007 [Important] Build from scratch or build on an existing library
**Why it matters:** the requirements name no alternatives. `Microsoft.Extensions.Caching.Hybrid` and FusionCache already handle loaders and stampede protection, and FusionCache has eager refresh. As far as I know, neither is built around write-through or write-behind to your database, so check this if you lean towards b.
**Options:** a) Own implementation on a Redis client  b) Read strategies on an existing library, own write strategies  c) This is partly a learning project, so build everything regardless
**Suggested:** a — one consistent model across all six strategies, unless learning is the main goal, in which case c.
**Answer:**
>

### Q-008 [Minor] Concrete examples
**Why it matters:** the requirements point to the diagrams for examples, but they contain no values, and section 4 is empty.
**Options:** a) I write examples into the brief for you to review (for example, reading product 42 with read-through)  b) You provide them
**Suggested:** a — you can correct them during review.
**Answer:**
>

**B. Cache store and data**

### Q-009 [Blocking] Cache stores supported in the first version
**Why it matters:** "Redis or some other caching database" could mean anything from one store to any store. `IDistributedCache` is the obvious shared abstraction, but it cannot read an entry's remaining TTL and has no primitives for a durable queue.
**Options:** a) Redis only, with no public extension point  b) Redis, plus a public store interface others can implement  c) Anything that implements `IDistributedCache`
**Suggested:** b — it honours "some other" without building a second store now.
**Answer:**
>

### Q-010 [Important] Redis deployment to support
**Why it matters:** Redis Cluster restricts multi-key operations to one hash slot, which affects batching and durable queues.
**Options:** a) Standalone or managed single endpoint only  b) Also Sentinel  c) Also Cluster
**Answer:**
>

### Q-011 [Important] Who owns the Redis connection
**Why it matters:** applications that already use Redis should not need a second connection. Other applications should not have to build one themselves.
**Options:** a) The library creates it from configuration  b) The application supplies an existing `IConnectionMultiplexer`  c) Either
**Suggested:** c — each is a small amount of code, and each suits a common setup.
**Answer:**
>

### Q-012 [Blocking] Value types and serialization
**Why it matters:** the requirements say only "values". This decides the API's shape and what ends up in Redis.
**Options:** a) Generic `T`, `System.Text.Json` by default, serializer replaceable  b) Generic `T`, JSON only  c) `byte[]` or `string` only; the caller serializes
**Suggested:** a — it is simple to use, and replacing the serializer also helps with Q-069.
**Answer:**
>

### Q-013 [Important] Cached data after a type changes
**Why it matters:** after a deployment changes a class, Redis still holds values in the old shape, and reading them may fail.
**Options:** a) Treat a value that cannot be read as a miss and log a warning; a configurable key prefix lets you version keys  b) Return an error to the caller
**Suggested:** a — a deployment should not turn old cache entries into failed reads.
**Answer:**
>

### Q-014 [Important] How keys are built
**Why it matters:** several applications sharing one Redis can overwrite each other's keys.
**Options:** a) Caller passes a string; the library adds a configured prefix  b) Caller passes a string, used as is  c) The library builds keys from the type and an id
**Suggested:** a — the caller keeps control, and applications stay separated.
**Answer:**
>

### Q-015 [Blocking] Where the TTL comes from
**Why it matters:** an entry without a TTL can stay stale forever. Refresh-ahead cannot work without one.
**Options:** a) Required default in configuration, with a per-write override  b) Caller passes it on every write  c) No TTL unless asked for
**Suggested:** a — no entry can be stored without an expiry by accident.
**Answer:**
>

### Q-016 [Important] Absolute or sliding expiry
**Why it matters:** the README defines refresh-ahead by age against TTL, which assumes absolute expiry. A sliding entry that is read often may never reload.
**Options:** a) Absolute only  b) Both, configurable  
**Suggested:** a — it is enough for all six strategies and limits staleness.
**Answer:**
>

### Q-017 [Blocking] Removing an entry
**Why it matters:** the requirements cover only read and write. Without a remove, the caller cannot invalidate an entry after the data changes or is deleted, and the README relies on invalidation for cache-aside and write-around.
**Options:** a) Add a remove capability  b) No remove; entries end only by TTL
**Suggested:** a — otherwise a deleted row can stay readable until its TTL expires.
**Answer:**
>

### Q-018 [Important] What a remove affects
**Why it matters:** this applies if Q-017 is a. If a write-behind for the key is still queued when the row is deleted, the queued write can bring the row back.
**Options:** a) Removes from the cache only, and cancels any queued write for the key  b) Follows the write strategy and also deletes from the database
**Suggested:** a — repositories keep control of deletes, and a deleted row cannot come back.
**Answer:**
>

### Q-019 [Minor] Batch reads and writes
**Why it matters:** the requirements describe single values, and batching adds API surface and Redis Cluster limits (Q-010).
**Options:** a) Single values only in the first version  b) Also batch operations
**Suggested:** a — nothing in the requirements asks for batches.
**Answer:**
>

**C. Reads**

### Q-020 [Blocking] Result of a read
**Why it matters:** the requirements report "failure" when no value is received. A miss is normal and an outage is not, and the caller must handle them differently.
**Options:** a) Three outcomes: value found, not found, error  b) Two outcomes: value or failure
**Suggested:** a — otherwise a repository cannot tell an empty result from an outage.
**Answer:**
>

### Q-021 [Blocking] Cache unavailable during a read
**Why it matters:** the README says cache-aside survives a cache outage, but with read-through "reads fail". Falling back keeps the application up, but during an outage all read traffic reaches the database.
**Options:** a) Fall back to the loader and log a warning, where a loader exists; otherwise return an error  b) Always return an error  c) Configurable per policy, defaulting to a
**Suggested:** a — the application stays available, and one behaviour is simpler. Choose b if your database could not take the full load.
**Answer:**
>

### Q-022 [Important] Database failure during a read miss
**Why it matters:** retries in the library can multiply load on a database that is already struggling.
**Options:** a) Return an error, no retries; retry logic belongs in the loader  b) Library retries as configured, then returns an error
**Suggested:** a — the loader knows which failures are safe to retry.
**Answer:**
>

### Q-023 [Important] Key not found in the database
**Why it matters:** repeated reads of a missing key reach the database every time. Caching "not found" protects the database, but can hide a row that is created later.
**Options:** a) Do not cache not-found results  b) Cache them with a short configurable TTL
**Suggested:** a — it is simpler and never hides new rows. Choose b if lookups of missing keys are common for you.
**Answer:**
>

### Q-024 [Important] Many callers missing the same key at once
**Why it matters:** the first read after a popular entry expires can send many identical queries to the database.
**Options:** a) No protection  b) One load per key, per process  c) One load per key across all instances, using a Redis lock
**Suggested:** b — it removes most of the load without the failure modes of a distributed lock.
**Answer:**
>

### Q-025 [Important] What triggers a refresh-ahead
**Why it matters:** the README says the cache "predicts" what is needed and mentions access tracking, but its steps only describe a refresh triggered by a read.
**Options:** a) A read of an entry past the refresh threshold starts a background reload  b) A background process tracks access and refreshes hot keys without a read
**Suggested:** a — it matches the README's steps and needs no tracking.
**Answer:**
>

### Q-026 [Minor] Refresh threshold
**Why it matters:** the README gives 75% of TTL only as an example.
**Options:** a) Configurable, default 75%  b) Fixed at 75%
**Suggested:** a — the README calls this a value that needs tuning.
**Answer:**
>

### Q-027 [Important] Refresh-ahead reload fails
**Why it matters:** the caller has already received a value, so nobody sees this failure directly.
**Options:** a) Keep serving the current value until its TTL ends, log a warning, and let a later read try again  b) Remove the entry at once  c) Extend the entry's TTL and keep serving stale data
**Suggested:** a — the TTL still limits how stale the data can get.
**Answer:**
>

**D. Writes**

### Q-028 [Blocking] What write success means
**Why it matters:** the requirements say success means the value is cached. Write-around does not cache it, and write-behind has not reached the database yet.
**Options:** a) Defined per strategy: write-through means both stores have it; write-behind means the cache has it and the database write is queued; write-around means the database has it  b) Always "the value is in the cache"
**Suggested:** a — b has no valid meaning for write-around.
**Answer:**
>

### Q-029 [Blocking] Write-through order
**Why it matters:** the README writes the cache first, then the database. If the database write then fails, readers can already have seen a value that was never saved.
**Options:** a) Cache first, as in the README; remove the entry if the database write fails  b) Database first, then cache
**Suggested:** b — readers never see unsaved data. This differs from your README, so please confirm.
**Answer:**
>

### Q-030 [Important] Write-through when the database succeeds but the cache fails
**Why it matters:** the data is saved, but the cache may still hold the old value until its TTL ends.
**Options:** a) Report success, log a warning, and try to remove the old entry  b) Report failure, even though the data is saved
**Suggested:** a — reporting failure invites retries of a write that already succeeded.
**Answer:**
>

### Q-031 [Blocking] The caller's database transaction
**Why it matters:** a repository may run inside a unit of work. A library writer on its own DI scope (Q-002) would not be part of that transaction, so a rollback can leave a value in the cache or database that the transaction discarded.
**Options:** a) Not supported; document that these writes run outside the caller's transaction  b) The caller commits, then tells the library to update or remove the entry  c) Join the ambient `TransactionScope`
**Suggested:** a — Redis cannot join a database transaction, so c could not make both stores atomic anyway. Write strategies with a registered writer then suit only writes that stand alone. If your repositories normally use a unit of work, tell me, because that affects Q-002 and Q-032.
**Answer:**
>

### Q-032 [Important] Who writes the database for write-around
**Why it matters:** the README says the application writes directly, but the point of the library is to hide the strategy from repositories.
**Options:** a) The library calls the registered writer, then handles the cache  b) The repository writes the database, then tells the library
**Suggested:** a — all write strategies then share one call.
**Answer:**
>

### Q-033 [Important] Cache handling after a write-around
**Why it matters:** the README allows both updating asynchronously and waiting for the next read. It also calls existing entries stale until they are handled.
**Options:** a) Remove the entry; the next read reloads it  b) Update it in the background with the written value  c) Leave it until the TTL ends
**Suggested:** a — it keeps the purpose of write-around (no cache pollution) and avoids stale reads.
**Answer:**
>

### Q-034 [Important] A read racing with a write
**Why it matters:** a reader can load an old row, a write and removal can then finish, and the reader can then store the old value. It stays stale until its TTL ends.
**Options:** a) Accept it, limited by the TTL, and document it  b) Prevent it with version checks
**Suggested:** a — the window is short, the TTL limits it, and b adds real complexity.
**Answer:**
>

### Q-035 [Important] Maximum acceptable staleness
**Why it matters:** other systems or manual fixes can change the database without the library knowing. Only the TTL corrects those changes, so this sets the upper limit for TTLs.
**Options:** Give a number, for example 5 minutes, or say that it varies by value type.
**Answer:**
>

**E. Write-behind**

### Q-036 [Blocking] Where queued writes are kept
**Why it matters:** this sets how much data a failure can lose. Writes queued in memory are lost on a crash, and also on a deployment unless they are flushed first. Redis has to do the work if a queue is to survive a restart.
**Options:** a) In process memory: simple, but a crash loses queued writes  b) In Redis: survives application restarts and can be drained by any instance, but adds queue operations to the store interface  c) Both, configurable
**Suggested:** b — it is the only option that holds up with many instances (Q-006). Loss is still possible if Redis itself loses data.
**Answer:**
>

### Q-037 [Important] When queued writes are flushed
**Why it matters:** a longer delay means more batching, but a longer window for possible loss and for the database being behind.
**Options:** a) Configurable maximum delay and batch size, defaulting to 5 seconds and 100 writes  b) Fixed values
**Suggested:** a — the right values depend on the workload. The defaults are starting points only.
**Answer:**
>

### Q-038 [Important] Several writes to one key before a flush
**Why it matters:** the README mentions coalescing. Keeping only the last value reduces database writes but loses the intermediate values.
**Options:** a) Keep only the latest value per key  b) Write every value in order
**Suggested:** a — it fits absolute values. Choose b if you need a full history.
**Answer:**
>

### Q-039 [Important] Order of queued writes
**Why it matters:** writes can reach the database out of order across keys. For example, an order line can arrive before the order it references, which breaks a foreign key.
**Options:** a) Order kept per key only, documented  b) Strict order across all keys
**Suggested:** a — b does not work with coalescing (Q-038) and limits throughput. Do not use write-behind for rows with foreign keys between them.
**Answer:**
>

### Q-040 [Blocking] A queued write keeps failing
**Why it matters:** the caller was told it succeeded and is gone by now, so the library alone decides what happens to the data.
**Options:** a) Retry with backoff, then move it to a dead-letter store, log an error and notify a configurable handler  b) Retry, then drop it and log an error  c) Retry forever
**Suggested:** a — no data is silently lost, and the queue cannot block forever on one bad write.
**Answer:**
>

### Q-041 [Important] Cached entry expires while its write is queued
**Why it matters:** a read would miss, load the older row from the database and return outdated data, even though the caller's write succeeded.
**Options:** a) Entries with a queued write do not expire until it completes  b) Accept the stale read and document it
**Suggested:** a — it protects read-your-own-write behaviour.
**Answer:**
>

### Q-042 [Important] Application shutdown with writes queued
**Why it matters:** a hosted service would need to drain the queue, so this adds a dependency on the host's lifecycle.
**Options:** a) Flush at shutdown, up to a configurable timeout  b) Do nothing and rely on the durable queue
**Suggested:** a — if Q-036 is a, it is the only protection a deployment has. If Q-036 is b, it reduces how far the database falls behind.
**Answer:**
>

### Q-043 [Important] Queue reaches its limit
**Why it matters:** if the database is down for long, an unbounded queue will eventually exhaust memory.
**Options:** a) Bounded; new writes return an error when full  b) Bounded; new writes wait for space  c) Unbounded
**Suggested:** a — callers find out immediately, and request threads are not held up.
**Answer:**
>

**F. Project setup**

### Q-044 [Blocking] Solution name
**Why it matters:** creates the solution file at the repository root.
**Options:** a) `CacheMe`  b) Another name
**Suggested:** a — matches the repository name.
**Answer:**
>

### Q-045 [Blocking] Project names
**Why it matters:** the library project name also becomes the default assembly and package name.
**Options:** a) `CacheMe` and `CacheMe.Tests`  b) Other names
**Suggested:** a — matches the folders of the earlier build output.
**Answer:**
>

### Q-046 [Blocking] Source folder
**Why it matters:** decides where the library code goes.
**Options:** a) `src/CacheMe`  b) Another folder
**Suggested:** a — matches `documentation/dotnet-cli.md`.
**Answer:**
>

### Q-047 [Blocking] Test folder
**Why it matters:** decides where the tests go.
**Options:** a) `tests/CacheMe.Tests`  b) Another folder
**Suggested:** a — matches `documentation/dotnet-cli.md`.
**Answer:**
>

### Q-048 [Blocking] Root namespace
**Why it matters:** appears in all consumer code, and is breaking to change later.
**Options:** a) `CacheMe`  b) Another namespace
**Suggested:** a — short, and matches the project.
**Answer:**
>

### Q-049 [Blocking] Target framework
**Why it matters:** decides which applications can use the library and which APIs it can use. The SDKs installed here are 8.0 and 10.0.
**Options:** a) `net10.0`  b) `net8.0`  c) Both
**Suggested:** a — .NET 10 is the current LTS, and .NET 8 support ends in November 2026. Choose c only if you have applications that must stay on .NET 8.
**Answer:**
>

### Q-050 [Important] C# language version
**Why it matters:** decides the language features available.
**Options:** a) The target framework's default (C# 14 for `net10.0`)  b) A pinned version
**Suggested:** a — it needs no maintenance and matches the SDK.
**Answer:**
>

### Q-051 [Important] Nullable reference types
**Why it matters:** the API must be clear on when a value can be missing, which is central to Q-020.
**Options:** a) On  b) Off
**Suggested:** a — it is the default for new projects and makes missing values visible to callers.
**Answer:**
>

### Q-052 [Important] Warnings as errors
**Why it matters:** stricter builds stop issues from accumulating, but they can also fail builds over new analyzer warnings.
**Options:** a) Yes  b) No
**Suggested:** a — easiest to adopt on a new project.
**Answer:**
>

### Q-053 [Blocking] Distribution
**Why it matters:** "any application using hexagonal architecture" suggests more than one consumer. NuGet needs package metadata, which I will ask for as a follow-up if you choose it.
**Options:** a) NuGet package  b) Project reference
**Suggested:** a — it supports separate applications and versioning.
**Answer:**
>

**G. Configuration and API**

### Q-054 [Important] Configuration source
**Why it matters:** decides how Stage 1 is shaped. Policies with delegates or types cannot be written in JSON alone.
**Options:** a) `appsettings.json`  b) Code  c) Both, with `IOptions<T>`, where code can override JSON
**Suggested:** c — settings stay in JSON, and types are registered in code.
**Answer:**
>

### Q-055 [Blocking] Microsoft dependency injection
**Why it matters:** the new DI scope in Q-002 and a hosted queue worker both depend on it.
**Options:** a) Yes, required  b) Optional  c) No
**Suggested:** a — it is simpler and fits the suggested answer to Q-002.
**Answer:**
>

### Q-056 [Blocking] Sync or async API
**Why it matters:** cache and database calls are network I/O.
**Options:** a) Async only  b) Sync and async  c) Sync only
**Suggested:** a — sync wrappers risk thread-pool starvation and double the API.
**Answer:**
>

### Q-057 [Important] `CancellationToken` support
**Why it matters:** callers need a way to stop waiting. Cancelling after a database write has started might not undo it, and a cancelled caller should not cancel a load that others share (Q-024).
**Options:** a) On every async method; it stops the caller's wait, and shared or background work continues  b) No support
**Suggested:** a — standard for .NET async APIs.
**Answer:**
>

### Q-058 [Important] Thread-safety
**Why it matters:** section 5 of the requirements is not filled in, and repositories in a web application are called concurrently.
**Options:** a) All public members are safe for concurrent use  b) Callers must synchronize
**Suggested:** a — a shared cache service is not usable otherwise.
**Answer:**
>

### Q-059 [Important] Service lifetime
**Why it matters:** the connection and any background work must outlive a request, but your loaders and writers often need scoped services.
**Options:** a) Library services are singletons; loaders and writers get a new scope per use  b) Scoped  c) Transient
**Suggested:** a — it fits Q-002.
**Answer:**
>

### Q-060 [Blocking] Reporting errors
**Why it matters:** the requirements ask for success or failure, which suggests result objects. Exceptions are the .NET default, though.
**Options:** a) Result objects for runtime outcomes; exceptions only for programmer errors and invalid configuration  b) Exceptions for all failures, with custom types  c) Result objects for everything
**Suggested:** a — it matches the requirements and makes outages hard to ignore.
**Answer:**
>

### Q-061 [Important] Input validation rules
**Why it matters:** section 3 asks what happens on invalid input.
**Options:** a) Reject empty keys, keys over 1,024 characters, null values and non-positive TTLs, as programmer errors under Q-060  b) Different rules
**Suggested:** a — it fails early. Storing null is also rejected, because it would look like a miss.
**Answer:**
>

### Q-062 [Important] Logging
**Why it matters:** several failures here are handled silently by design (Q-021, Q-027, Q-030), so logs are the only evidence.
**Options:** a) `ILogger<T>`  b) None
**Suggested:** a — without it those failures are invisible.
**Answer:**
>

### Q-063 [Important] Metrics and tracing
**Why it matters:** the hit rate shows whether a cache is working, and queue depth shows whether write-behind is keeping up.
**Options:** a) Logs only  b) Metrics through `System.Diagnostics.Metrics` (hits, misses, errors, queue depth)  c) Metrics and `Activity` tracing
**Suggested:** b — it answers those two questions at little cost.
**Answer:**
>

**H. Dependencies, performance and security**

### Q-064 [Important] Redis timeouts and retries
**Why it matters:** section 6 is empty. A slow cache must not be slower than having no cache at all.
**Options:** a) Configurable, default 1 second per operation, no library retries on reads  b) Client defaults  c) Other values
**Suggested:** a — a read can fall back quickly under Q-021.
**Answer:**
>

### Q-065 [Important] Clock
**Why it matters:** refresh thresholds, retries and flush delays depend on time, and tests should not have to wait for it.
**Options:** a) `TimeProvider`  b) Own clock interface  c) `DateTime` directly
**Suggested:** a — it is built into .NET and can be replaced in tests.
**Answer:**
>

### Q-066 [Important] Performance target
**Why it matters:** the requirements give no target. Most of a call's time is Redis and the network, so only the library's own overhead can be held to a number.
**Options:** a) Under 1 ms p99 overhead beyond the Redis round trip and serialization, at 1,000 operations per second per instance  b) Your own numbers  c) No numeric target
**Suggested:** a — a placeholder until you have real traffic figures.
**Answer:**
>

### Q-067 [Important] Maximum value size
**Why it matters:** Redis allows up to 512 MB, but large values slow every other command on the same connection.
**Options:** a) Configurable, default 1 MB; larger writes are rejected  b) No limit
**Suggested:** a — it catches accidental huge values early.
**Answer:**
>

### Q-068 [Important] Connection secrets
**Why it matters:** the Redis connection string holds a password.
**Options:** a) Supplied through standard configuration, never logged, TLS supported  b) Also require TLS
**Suggested:** a — the application's own secret store then applies, and local development can run without TLS.
**Answer:**
>

### Q-069 [Important] Sensitive values in the cache
**Why it matters:** values in Redis are readable by anyone with access to it. Personal or financial data may need protection.
**Options:** a) No encryption in the library; document that callers should not cache sensitive data, or should use an encrypting serializer  b) Built-in encryption
**Suggested:** a — unless you already know you will cache sensitive data, in which case b.
**Answer:**
>

### Q-070 [Important] Keys in logs
**Why it matters:** keys often contain identifiers such as email addresses or customer ids.
**Options:** a) Log keys at Debug level only  b) Always log keys  c) Never log keys
**Suggested:** a — production logs stay clean, and diagnosis remains possible.
**Answer:**
>

### Q-071 [Important] Compatibility and versioning
**Why it matters:** no callers exist today, so nothing has to keep working. The first release sets the rule from then on.
**Options:** a) Semantic Versioning, starting at 1.0.0, with no compatibility constraints before that  b) Another scheme
**Suggested:** a — standard for NuGet packages.
**Answer:**
>

**I. Tests, documents and limits**

### Q-072 [Important] xUnit version
**Why it matters:** xUnit v2 and v3 are separate packages. The default for this workflow is the latest stable.
**Options:** a) Latest stable xUnit v3  b) Latest stable xUnit v2
**Suggested:** a — it follows the workflow default.
**Answer:**
>

### Q-073 [Important] Moq version
**Why it matters:** the test project needs a specific version.
**Options:** a) Latest stable  b) A specific version
**Suggested:** a — it follows the workflow default.
**Answer:**
>

### Q-074 [Important] Tests against a real Redis
**Why it matters:** Moq can check the library's logic, but it cannot prove TTL behaviour or durable queue behaviour against real Redis. The workflow plans for xUnit and Moq only.
**Options:** a) Moq only  b) Moq, plus integration tests using Redis in a container (needs Docker)  c) Moq, plus integration tests against a local Redis
**Suggested:** b — the riskiest behaviour depends on Redis, and a container is easy to repeat.
**Answer:**
>

### Q-075 [Minor] Consumer README
**Why it matters:** decides whether the final stage produces one.
**Options:** a) Yes  b) No
**Suggested:** a — the strategies have trade-offs that consumers need to know about.
**Answer:**
>

### Q-076 [Important] Out of scope
**Why it matters:** section 8 is empty. People may assume a caching library does these things.
**Options:** a) All out of scope: an in-process (L1) cache layer, tag-based invalidation, preloading the cache at startup, cross-instance change notifications, cross-region replication  b) Keep some in scope (say which)
**Suggested:** a — none of them is in the requirements.
**Answer:**
>

### Q-077 [Minor] Outside constraints
**Why it matters:** section 9 is empty. Company policy, approved packages or licensing could rule out choices made above.
**Options:** a) None  b) Yes (list them)
**Suggested:** a — only if you know of none.
**Answer:**
>

## Decisions

None yet.

## Out of scope

Nothing is confirmed yet. See Q-076.
