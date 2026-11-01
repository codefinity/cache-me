# Library Analysis

| Field       | Value                 |
|-------------|-----------------------|
| Stage       | 0 – Analysis          |
| Version     | v3                    |
| Date        | 2026-11-01            |
| Status      | Approved              |
| Approved on | 2026-11-01            |
| Based on    | lib-requirements.md   |
| Supersedes  | v2                    |

## Changelog
- v3: Recorded the answer "select suggested" for the 13 questions open in v2. Moved 12 to Decisions and closed Q-088 under Assumptions. Added the Library Design Brief, because no question remains open.

## Requirements as understood

The library lets an application cache values in Redis, or another store through a public interface, without writing the caching logic itself. It implements the six strategies in the root `README.md`:

- **Read:** cache-aside, read-through, refresh-ahead (as an add-on to read-through).
- **Write:** write-through, write-behind, write-around.

The caller is the repository layer of an application built with hexagonal architecture, running as many instances. Repositories call an async API. Each value type has one policy, set at registration, rather than a strategy passed on each call as the requirements first said (Q-003, Q-079). The library reaches the database through registered loaders and writers, each resolved in a new DI scope.

A read returns a value, not found, or an error. What a successful write means depends on the strategy (Q-028).

## Assumptions

**Closed [Minor] questions, with your agreement ("select suggested")**

- Q-008: I write the concrete examples into the brief, and you review them.
- Q-019: Single values only in the first version, with no batch operations.
- Q-026: The refresh threshold is configurable and defaults to 75% of TTL.
- Q-075: A consumer README is produced at the end.
- Q-077: There are no outside constraints from company policy, approved packages or licensing.
- Q-088: Package metadata is authors `Nikhil P`; license `MIT`; repository `https://github.com/codefinity/cache-me`; description "Read and write caching strategies for .NET, with Redis support."

**Carried from earlier versions**

- The full set of strategies is the six in the root `README.md`, because the requirements refer to that list without narrowing it.
- There are no existing callers. The repository has no `.sln`, `.slnx`, `.csproj` or `.cs` files, and none in its git history.
- `src/CacheMe` and `tests/CacheMe.Tests` contain only ignored build output.

**Consequences of the decisions below, for you to correct if they are wrong**

- Writes made through a write strategy do not need to share a unit of work with other database changes (Q-031).
- Learning is not the main goal of this project (Q-007).
- Refresh-ahead uses the same per-process protection as read misses (Q-024), so each instance can reload the same key once when it passes the threshold.
- Q-013 applies to cached entries only. A queued write that cannot be read after a type change goes to the dead-letter store (Q-040) rather than being treated as a miss.
- Numeric defaults not set in this stage (default TTL, retry attempts and backoff, queue limit, dead-letter retention) are decided in Stage 1.
- Docker is available on this machine for the integration tests in Q-074 (checked: version 29.9.2, server running).

## Open Questions

None.

## Decisions

Every answer below was given in chat, as the instruction "select suggested".

### Q-001 Difference between cache-aside and read-through
**Question:** How do cache-aside and read-through differ when a library does the work?
**Answer:** select suggested (answered in chat)
**Interpretation:** Cache-aside exposes plain get and set, and the caller handles the miss; read-through takes a loader and handles the miss itself.

### Q-002 How the library reaches the database
**Question:** How does the library get database access?
**Answer:** select suggested (answered in chat)
**Interpretation:** Registered loader and writer types per value type, resolved in a new DI scope for each use.

### Q-003 Where the strategy is chosen
**Question:** Is the strategy chosen per call, at registration, or both?
**Answer:** select suggested (answered in chat)
**Interpretation:** At registration, replacing the per-call choice written in the requirements.

### Q-004 Refresh-ahead as a strategy or an add-on
**Question:** Is refresh-ahead standalone or an add-on?
**Answer:** select suggested (answered in chat)
**Interpretation:** An add-on to read-through, which handles the miss.

### Q-005 Allowed read and write combinations
**Question:** Which read and write strategy pairs are allowed?
**Answer:** select suggested (answered in chat)
**Interpretation:** Any read strategy with any write strategy.

### Q-006 One application instance or many
**Question:** Does the application run as one instance or many?
**Answer:** select suggested (answered in chat)
**Interpretation:** Many instances behind a load balancer.

### Q-007 Build from scratch or build on an existing library
**Question:** Should the library build on an existing caching library?
**Answer:** select suggested (answered in chat)
**Interpretation:** Own implementation on a Redis client.

### Q-009 Cache stores supported in the first version
**Question:** Which cache stores does the first version support?
**Answer:** select suggested (answered in chat)
**Interpretation:** Redis, plus a public store interface others can implement.

### Q-010 Redis deployment to support
**Question:** Which Redis deployments are supported?
**Answer:** select suggested (answered in chat)
**Interpretation:** Standalone or managed single endpoint only, with no Sentinel or Cluster support.

### Q-011 Who owns the Redis connection
**Question:** Does the library or the application own the Redis connection?
**Answer:** select suggested (answered in chat)
**Interpretation:** Either: the library creates it from configuration, or the application supplies an `IConnectionMultiplexer`.

### Q-012 Value types and serialization
**Question:** What value types are supported, and how are they serialized?
**Answer:** select suggested (answered in chat)
**Interpretation:** Generic `T`, `System.Text.Json` by default, serializer replaceable.

### Q-013 Cached data after a type changes
**Question:** What happens when a cached value can no longer be read after a type changes?
**Answer:** select suggested (answered in chat)
**Interpretation:** Treat it as a miss and log a warning; a configurable key prefix allows key versioning.

### Q-014 How keys are built
**Question:** Who builds cache keys?
**Answer:** select suggested (answered in chat)
**Interpretation:** The caller passes a string, and the library adds a configured prefix.

### Q-015 Where the TTL comes from
**Question:** Where does an entry's TTL come from?
**Answer:** select suggested (answered in chat)
**Interpretation:** A required default in configuration, with a per-write override.

### Q-016 Absolute or sliding expiry
**Question:** Is expiry absolute, sliding or both?
**Answer:** select suggested (answered in chat)
**Interpretation:** Absolute only.

### Q-017 Removing an entry
**Question:** Does the library offer a remove operation?
**Answer:** select suggested (answered in chat)
**Interpretation:** Yes, a remove capability is added to the requirements.

### Q-018 What a remove affects
**Question:** What does a remove affect?
**Answer:** select suggested (answered in chat)
**Interpretation:** The cache only, and it cancels any queued write for the key.

### Q-020 Result of a read
**Question:** What outcomes can a read return?
**Answer:** select suggested (answered in chat)
**Interpretation:** Three outcomes: value found, not found, error.

### Q-021 Cache unavailable during a read
**Question:** What happens to a read when the cache is unavailable?
**Answer:** select suggested (answered in chat)
**Interpretation:** Fall back to the loader and log a warning where a loader exists; otherwise return an error.

### Q-022 Database failure during a read miss
**Question:** What happens when the database fails during a read miss?
**Answer:** select suggested (answered in chat)
**Interpretation:** Return an error with no library retries; retry logic belongs in the loader.

### Q-023 Key not found in the database
**Question:** Are not-found results cached?
**Answer:** select suggested (answered in chat)
**Interpretation:** No.

### Q-024 Many callers missing the same key at once
**Question:** How are concurrent misses on one key handled?
**Answer:** select suggested (answered in chat)
**Interpretation:** One load per key, per process.

### Q-025 What triggers a refresh-ahead
**Question:** What starts a refresh-ahead reload?
**Answer:** select suggested (answered in chat)
**Interpretation:** A read of an entry past the refresh threshold starts a background reload.

### Q-027 Refresh-ahead reload fails
**Question:** What happens when a background reload fails?
**Answer:** select suggested (answered in chat)
**Interpretation:** Keep serving the current value until its TTL ends, log a warning, and let a later read try again.

### Q-028 What write success means
**Question:** What does a successful write mean?
**Answer:** select suggested (answered in chat)
**Interpretation:** Per strategy: write-through means both stores have it; write-behind means the cache has it and the database write is queued; write-around means the database has it.

### Q-029 Write-through order
**Question:** In what order does write-through update the two stores?
**Answer:** select suggested (answered in chat)
**Interpretation:** Database first, then cache, which differs from the order in the README.

### Q-030 Write-through when the database succeeds but the cache fails
**Question:** What is reported when the database write succeeds but the cache write fails?
**Answer:** select suggested (answered in chat)
**Interpretation:** Report success, log a warning, and try to remove the old entry.

### Q-031 The caller's database transaction
**Question:** Do writes take part in the caller's database transaction?
**Answer:** select suggested (answered in chat)
**Interpretation:** No; this is documented as not supported.

### Q-032 Who writes the database for write-around
**Question:** Who writes the database for write-around?
**Answer:** select suggested (answered in chat)
**Interpretation:** The library calls the registered writer, then handles the cache.

### Q-033 Cache handling after a write-around
**Question:** What happens to the cache entry after a write-around?
**Answer:** select suggested (answered in chat)
**Interpretation:** Remove the entry; the next read reloads it.

### Q-034 A read racing with a write
**Question:** Is the race where a slow reader stores an old value prevented?
**Answer:** select suggested (answered in chat)
**Interpretation:** No; it is accepted, limited by the TTL, and documented.

### Q-035 Maximum acceptable staleness
**Question:** How stale can cached data be after an outside change to the database?
**Answer:** select suggested (answered in chat)
**Interpretation:** 5 minutes for all value types, adjustable per type, as a placeholder.

### Q-036 Where queued writes are kept
**Question:** Where are write-behind's queued writes kept?
**Answer:** select suggested (answered in chat)
**Interpretation:** In Redis.

### Q-037 When queued writes are flushed
**Question:** When are queued writes flushed?
**Answer:** select suggested (answered in chat)
**Interpretation:** Configurable maximum delay and batch size, defaulting to 5 seconds and 100 writes.

### Q-038 Several writes to one key before a flush
**Question:** What happens to several queued writes to one key?
**Answer:** select suggested (answered in chat)
**Interpretation:** Only the latest value per key is kept.

### Q-039 Order of queued writes
**Question:** What write order is guaranteed?
**Answer:** select suggested (answered in chat)
**Interpretation:** Order per key only, documented.

### Q-040 A queued write keeps failing
**Question:** What happens when a queued write keeps failing?
**Answer:** select suggested (answered in chat)
**Interpretation:** Retry with backoff, then move it to a dead-letter store, log an error and notify a configurable handler.

### Q-041 Cached entry expires while its write is queued
**Question:** Can an entry expire while its write is queued?
**Answer:** select suggested (answered in chat)
**Interpretation:** No; it does not expire until the write completes.

### Q-042 Application shutdown with writes queued
**Question:** What happens to queued writes at shutdown?
**Answer:** select suggested (answered in chat)
**Interpretation:** Flush at shutdown, up to a configurable timeout.

### Q-043 Queue reaches its limit
**Question:** What happens when the queue is full?
**Answer:** select suggested (answered in chat)
**Interpretation:** The queue is bounded, and new writes return an error when it is full.

### Q-044 Solution name
**Question:** What is the solution name?
**Answer:** select suggested (answered in chat)
**Interpretation:** `CacheMe`.

### Q-045 Project names
**Question:** What are the project names?
**Answer:** select suggested (answered in chat)
**Interpretation:** `CacheMe` and `CacheMe.Tests`.

### Q-046 Source folder
**Question:** Where does the library source go?
**Answer:** select suggested (answered in chat)
**Interpretation:** `src/CacheMe`.

### Q-047 Test folder
**Question:** Where do the tests go?
**Answer:** select suggested (answered in chat)
**Interpretation:** `tests/CacheMe.Tests`.

### Q-048 Root namespace
**Question:** What is the root namespace?
**Answer:** select suggested (answered in chat)
**Interpretation:** `CacheMe`.

### Q-049 Target framework
**Question:** Which framework does the library target?
**Answer:** select suggested (answered in chat)
**Interpretation:** `net10.0`.

### Q-050 C# language version
**Question:** Which C# version is used?
**Answer:** select suggested (answered in chat)
**Interpretation:** The target framework's default, C# 14.

### Q-051 Nullable reference types
**Question:** Are nullable reference types on?
**Answer:** select suggested (answered in chat)
**Interpretation:** On.

### Q-052 Warnings as errors
**Question:** Are warnings treated as errors?
**Answer:** select suggested (answered in chat)
**Interpretation:** Yes.

### Q-053 Distribution
**Question:** How is the library distributed?
**Answer:** select suggested (answered in chat)
**Interpretation:** As a NuGet package; details are in Q-086 to Q-088.

### Q-054 Configuration source
**Question:** Where does configuration come from?
**Answer:** select suggested (answered in chat)
**Interpretation:** Both `appsettings.json` and code, with `IOptions<T>`, where code can override JSON.

### Q-055 Microsoft dependency injection
**Question:** Is Microsoft dependency injection used?
**Answer:** select suggested (answered in chat)
**Interpretation:** Yes, required.

### Q-056 Sync or async API
**Question:** Is the API sync, async or both?
**Answer:** select suggested (answered in chat)
**Interpretation:** Async only.

### Q-057 `CancellationToken` support
**Question:** Is `CancellationToken` supported?
**Answer:** select suggested (answered in chat)
**Interpretation:** On every async method; it stops the caller's wait, and shared or background work continues.

### Q-058 Thread-safety
**Question:** Is the library thread-safe?
**Answer:** select suggested (answered in chat)
**Interpretation:** All public members are safe for concurrent use.

### Q-059 Service lifetime
**Question:** What lifetime do the services have?
**Answer:** select suggested (answered in chat)
**Interpretation:** Library services are singletons; loaders and writers get a new scope per use.

### Q-060 Reporting errors
**Question:** How are errors reported?
**Answer:** select suggested (answered in chat)
**Interpretation:** Result objects for runtime outcomes; exceptions only for programmer errors and invalid configuration.

### Q-061 Input validation rules
**Question:** What input is rejected?
**Answer:** select suggested (answered in chat)
**Interpretation:** Empty keys, keys over 1,024 characters, null values and non-positive TTLs, as programmer errors.

### Q-062 Logging
**Question:** Does the library log?
**Answer:** select suggested (answered in chat)
**Interpretation:** Yes, through `ILogger<T>`.

### Q-063 Metrics and tracing
**Question:** What diagnostics does the library provide besides logs?
**Answer:** select suggested (answered in chat)
**Interpretation:** Metrics through `System.Diagnostics.Metrics` (hits, misses, errors, queue depth), with no tracing.

### Q-064 Redis timeouts and retries
**Question:** What timeouts and retries apply to Redis?
**Answer:** select suggested (answered in chat)
**Interpretation:** Configurable, default 1 second per operation, no library retries on reads.

### Q-065 Clock
**Question:** How does the library read the time?
**Answer:** select suggested (answered in chat)
**Interpretation:** Through `TimeProvider`.

### Q-066 Performance target
**Question:** What is the performance target?
**Answer:** select suggested (answered in chat)
**Interpretation:** Under 1 ms p99 overhead beyond the Redis round trip and serialization, at 1,000 operations per second per instance, as a placeholder.

### Q-067 Maximum value size
**Question:** What is the maximum value size?
**Answer:** select suggested (answered in chat)
**Interpretation:** Configurable, default 1 MB; larger writes are rejected.

### Q-068 Connection secrets
**Question:** How are Redis connection secrets handled?
**Answer:** select suggested (answered in chat)
**Interpretation:** Supplied through standard configuration, never logged, TLS supported but not required.

### Q-069 Sensitive values in the cache
**Question:** Does the library encrypt cached values?
**Answer:** select suggested (answered in chat)
**Interpretation:** No; documentation tells callers not to cache sensitive data, or to use an encrypting serializer.

### Q-070 Keys in logs
**Question:** Are keys written to logs?
**Answer:** select suggested (answered in chat)
**Interpretation:** At Debug level only.

### Q-071 Compatibility and versioning
**Question:** What are the compatibility and versioning rules?
**Answer:** select suggested (answered in chat)
**Interpretation:** Semantic Versioning, starting at 1.0.0, with no compatibility constraints before that.

### Q-072 xUnit version
**Question:** Which xUnit version is used?
**Answer:** select suggested (answered in chat)
**Interpretation:** Latest stable xUnit v3.

### Q-073 Moq version
**Question:** Which Moq version is used?
**Answer:** select suggested (answered in chat)
**Interpretation:** Latest stable.

### Q-074 Tests against a real Redis
**Question:** Are there tests against a real Redis?
**Answer:** select suggested (answered in chat)
**Interpretation:** Moq, plus integration tests using Redis in a container.

### Q-076 Out of scope
**Question:** What is explicitly out of scope?
**Answer:** select suggested (answered in chat)
**Interpretation:** All five listed items; see Out of scope below.

### Q-078 (follow-up to Q-001) Storing a value after a cache-aside miss
**Question:** How does a caller store a value after a cache-aside miss without triggering a write strategy?
**Answer:** select suggested (answered in chat)
**Interpretation:** Two operations: a cache-only store that never triggers a write strategy, and a write that follows it.

### Q-079 (follow-up to Q-003) What a strategy is registered against
**Question:** What is a strategy registered against?
**Answer:** select suggested (answered in chat)
**Interpretation:** One policy per value type.

### Q-080 (follow-up to Q-003) Read-only or write-only policies
**Question:** Must a policy set both a read and a write strategy?
**Answer:** select suggested (answered in chat)
**Interpretation:** No; a policy sets either or both, and calling the missing side is a programmer error.

### Q-081 (follow-up to Q-041) Refresh-ahead while a write is queued
**Question:** Does a refresh-ahead reload run while the key has a queued write?
**Answer:** select suggested (answered in chat)
**Interpretation:** No; the refresh is skipped while a write is queued.

### Q-082 (follow-up to Q-036) Redis settings the durable queue relies on
**Question:** How does the library handle Redis settings that make the durable queue unsafe?
**Answer:** select suggested (answered in chat)
**Interpretation:** Document the required settings and log a startup warning when settings are readable and unsafe.

### Q-083 (follow-up to Q-036) Cache unavailable during a write-behind write
**Question:** What happens to a write-behind write when the cache is unavailable?
**Answer:** select suggested (answered in chat)
**Interpretation:** Return an error; nothing is queued.

### Q-084 (follow-up to Q-033) Write-around when the database succeeds but the cache fails
**Question:** What is reported when the database write succeeds but the entry cannot be removed?
**Answer:** select suggested (answered in chat)
**Interpretation:** Report success and log a warning, the same as Q-030.

### Q-085 (follow-up to Q-040) Dead-letter store
**Question:** Where does the dead-letter store live, and how long does it keep writes?
**Answer:** select suggested (answered in chat)
**Interpretation:** In Redis, with configurable retention; your handler decides what to do with each write, and the library does not replay them.

### Q-086 Where the package is published
**Question:** Where is the NuGet package published?
**Answer:** select suggested (answered in chat)
**Interpretation:** Build the package only; decide on publishing later.

### Q-087 Package ID
**Question:** What is the package ID?
**Answer:** select suggested (answered in chat)
**Interpretation:** `CacheMe`.

## Out of scope

The final list is in the Library Design Brief below.

## Library Design Brief

### Purpose and consumers

CacheMe is a .NET library that gives an application the six caching strategies in the root `README.md` without the application having to write the caching logic.

It is called from the repository layer of applications built with hexagonal architecture, running as many instances. Those applications choose a policy for each value type at registration, so repository code does not change when the policy does.

### Functional requirements

**Policies**

| ID     | Requirement | Source |
|--------|-------------|--------|
| FR-001 | Each value type has one policy, set at registration, with a read strategy, a write strategy or both. | Q-003, Q-079, Q-080 |
| FR-002 | Any read strategy can be combined with any write strategy. | Q-005 |
| FR-003 | Calling a read or write operation that the value type's policy does not define is a programmer error. | Q-080, Q-060 |

**Reads**

| ID     | Requirement | Source |
|--------|-------------|--------|
| FR-004 | A read returns one of three outcomes: value found, not found, or error. | lib-requirements.md, Q-020 |
| FR-005 | Cache-aside: a read checks only the cache, and on a miss the caller loads the value and stores it. | README, Q-001 |
| FR-006 | The cache-only store operation never triggers a write strategy. | Q-078 |
| FR-007 | Read-through: on a miss the library calls the registered loader, stores the result and returns it. | README, Q-001, Q-002 |
| FR-008 | Refresh-ahead is an add-on to read-through. A read of an entry past the threshold returns the current value and starts a background reload. | README, Q-004, Q-025 |
| FR-009 | The refresh threshold is configurable and defaults to 75% of TTL. | Q-026 |
| FR-010 | No refresh runs for a key with a queued write. | Q-081 |
| FR-011 | If a refresh fails, the current value is served until its TTL ends, a warning is logged, and a later read can try again. | Q-027 |
| FR-012 | If the cache is unavailable during a read, the library falls back to the loader and logs a warning. With no loader, the read returns an error. | Q-021 |
| FR-013 | If the loader fails on a miss, the read returns an error, and the library does not retry. | Q-022 |
| FR-014 | A not-found result from the loader is not cached. | Q-023 |
| FR-015 | Concurrent misses on one key cause one load per key, per process. | Q-024 |
| FR-016 | A cached entry that cannot be read is treated as a miss, and a warning is logged. | Q-013 |

**Writes**

| ID     | Requirement | Source |
|--------|-------------|--------|
| FR-017 | Write-through writes the database through the registered writer, then the cache. Success means both stores have the value. | README, Q-028, Q-029 |
| FR-018 | If a write-through's database write fails, the write returns an error and the cache is not updated. | Q-029 |
| FR-019 | If a write-through's database write succeeds but its cache write fails, the write reports success, logs a warning and tries to remove the old entry. | Q-030 |
| FR-020 | Write-behind stores the value in the cache and queues the database write in Redis. Success means both have happened. | README, Q-028, Q-036 |
| FR-021 | If the cache is unavailable during a write-behind write, the write returns an error and nothing is queued. | Q-083 |
| FR-022 | Queued writes are flushed by a configurable maximum delay and batch size, defaulting to 5 seconds and 100 writes. | Q-037 |
| FR-023 | Only the latest queued value per key is kept. | Q-038 |
| FR-024 | Write order is kept per key only. | Q-039 |
| FR-025 | A queued write that keeps failing is retried with backoff, then moved to the dead-letter store. An error is logged, and a configurable handler is notified. | Q-040 |
| FR-026 | The dead-letter store is in Redis with configurable retention. The library does not replay dead-letter writes. | Q-085 |
| FR-027 | An entry with a queued write does not expire until the write completes. | Q-041 |
| FR-028 | Queued writes are flushed at application shutdown, up to a configurable timeout. | Q-042 |
| FR-029 | The queue is bounded, and new writes return an error when it is full. | Q-043 |
| FR-030 | Write-around writes the database through the registered writer, then removes the cache entry. Success means the database has the value. | README, Q-028, Q-032, Q-033 |
| FR-031 | If a write-around's database write succeeds but the entry cannot be removed, the write reports success and logs a warning. | Q-084 |

**Removal, keys, expiry and values**

| ID     | Requirement | Source |
|--------|-------------|--------|
| FR-032 | A remove operation removes an entry from the cache only, and cancels any queued write for the key. | Q-017, Q-018 |
| FR-033 | The caller passes a string key, and the library adds a configured prefix. | Q-014 |
| FR-034 | Every entry has an absolute TTL: a required default in configuration, with a per-write override. | Q-015, Q-016 |
| FR-035 | Values are generic `T`, serialized with `System.Text.Json` by default, with a replaceable serializer. | Q-012 |
| FR-036 | Redis is supported, along with a public store interface that others can implement. | lib-requirements.md, Q-009 |
| FR-037 | Empty keys, keys over 1,024 characters, null values and non-positive TTLs are rejected as programmer errors. | Q-061 |
| FR-038 | Writes larger than the maximum value size are rejected. | Q-067 |

### Examples

These examples use the key prefix `shop:` and a 5-minute TTL. The value is product 42: `{ "id": 42, "name": "Keyboard", "price": 49.99 }`. The caller passes the key `product:42`, which is stored as `shop:product:42`.

| Strategy | Situation | Result |
|----------|-----------|--------|
| Cache-aside | Read of `product:42`; the entry is missing | Not found. The caller loads the product and uses the cache-only store. |
| Read-through | Read of `product:42`; the entry is missing and the row exists | The loader runs, and the entry is stored with a 5-minute TTL. The read returns value found. |
| Read-through | Read of `product:99`; the entry is missing and no row exists | Not found. Nothing is cached, so the next read runs the loader again. |
| Read-through | Read of `product:42` with Redis down | The loader runs, a warning is logged, and the read returns value found. |
| Refresh-ahead | Read of `product:42` when the entry is 4 minutes old (threshold is 3 minutes 45 seconds) | The current value returns at once, and a background reload starts. |
| Write-through | Price changes to 44.99 | The database is written, then the cache. The write reports success, and the next read returns 44.99. |
| Write-through | Price changes to 44.99 and the database rejects it | An error is returned, and the cache still holds 49.99. |
| Write-behind | Price changes to 44.99, then to 39.99, within one flush | Both writes report success. Reads return 39.99, and the database receives only 39.99. |
| Write-behind | A queued write still fails after its final retry | The write moves to the dead-letter store, an error is logged, and the handler is notified. |
| Write-around | Price changes to 44.99 | The database is written, and the entry is removed. The next read misses and gets 44.99 from the loader. |

### Non-functional requirements

| ID      | Requirement | Measure or limit | Source |
|---------|-------------|------------------|--------|
| NFR-001 | Library overhead (placeholder until real traffic figures exist) | Under 1 ms p99 beyond the Redis round trip and serialization, at 1,000 operations per second per instance | Q-066 |
| NFR-002 | Maximum staleness after an outside database change (placeholder) | 5 minutes for all value types, adjustable per type | Q-035 |
| NFR-003 | Redis operation timeout | Configurable, default 1 second, with no library retries on reads | Q-064 |
| NFR-004 | Maximum value size | Configurable, default 1 MB | Q-067 |
| NFR-005 | Concurrency | All public members are safe for concurrent use | Q-058 |
| NFR-006 | Multiple instances | Correct behaviour when many instances share one Redis | Q-006 |
| NFR-007 | Write-behind durability | Queued writes survive application restarts, and any instance can drain them. Loss is possible if Redis loses data. | Q-036, Q-082 |
| NFR-008 | Observability | Metrics for hits, misses, errors and queue depth | Q-063 |

### Technical decisions

| ID     | Topic | Decision | Source |
|--------|-------|----------|--------|
| TD-001 | Solution and project names, source and test folders, root namespace | Solution `CacheMe`; projects `CacheMe` and `CacheMe.Tests`; folders `src/CacheMe` and `tests/CacheMe.Tests`; root namespace `CacheMe` | Q-044, Q-045, Q-046, Q-047, Q-048 |
| TD-002 | Target framework(s), C# version, nullable, warnings-as-errors | `net10.0`; C# 14 (framework default); nullable on; warnings as errors | Q-049, Q-050, Q-051, Q-052 |
| TD-003 | Distribution and package metadata | NuGet package with ID `CacheMe`, built but not published yet; authors `Nikhil P`; license `MIT`; repository `https://github.com/codefinity/cache-me`; description "Read and write caching strategies for .NET, with Redis support." | Q-053, Q-086, Q-087, Q-088 |
| TD-004 | Configuration approach and dependency injection | `appsettings.json` and code through `IOptions<T>`, where code can override JSON; Microsoft dependency injection required | Q-054, Q-055 |
| TD-005 | Sync vs async, `CancellationToken` | Async only; `CancellationToken` on every async method, which stops the caller's wait while shared or background work continues | Q-056, Q-057 |
| TD-006 | Thread-safety and service lifetime | Thread-safe; library services are singletons; loaders and writers get a new DI scope per use | Q-058, Q-059, Q-002 |
| TD-007 | Error handling and input validation | Result objects for runtime outcomes; exceptions only for programmer errors and invalid configuration; validation as in FR-037 and FR-038 | Q-060, Q-061, Q-067 |
| TD-008 | Logging and diagnostics | `ILogger<T>`; keys logged at Debug level only; metrics through `System.Diagnostics.Metrics`; no tracing | Q-062, Q-063, Q-070 |
| TD-009 | External dependencies (the Stage 2 seams) | Cache store (public interface, with Redis on `StackExchange.Redis`, using a library-created connection or an application-supplied `IConnectionMultiplexer`); database (registered loaders and writers); serializer; clock (`TimeProvider`); dead-letter handler. Redis is a single endpoint only. | Q-002, Q-007, Q-009, Q-010, Q-011, Q-012, Q-040, Q-065 |
| TD-010 | Performance targets, limits, resource constraints | As in NFR-001 to NFR-004; flush defaults as in FR-022; bounded queue as in FR-029 | Q-064, Q-066, Q-067, Q-037, Q-043 |
| TD-011 | Security and secrets handling | Connection secrets through standard configuration, never logged; TLS supported but not required; no built-in encryption, so documentation tells callers not to cache sensitive data or to use an encrypting serializer | Q-068, Q-069, Q-070 |
| TD-012 | Backward compatibility and versioning | Semantic Versioning, starting at 1.0.0, with no compatibility constraints before that | Q-071 |
| TD-013 | xUnit and Moq versions | Latest stable xUnit v3 and latest stable Moq, plus integration tests using Redis in a container | Q-072, Q-073, Q-074 |
| TD-014 | Consumer-facing README wanted | Yes | Q-075 |

### Constraints

- Callers are the repository layer of hexagonal applications (lib-requirements.md).
- Redis does not call the database, so all strategy logic runs inside the library, including background work (Q-002).
- A write-behind queue needs Redis settings that keep its data: persistence, and an eviction policy that does not remove queue keys. These are documented, and unsafe settings cause a startup warning when they can be read (Q-082).
- The integration tests need Docker (Q-074).

### Behaviour the consumer README must document

- Writes run outside the caller's database transaction (Q-031).
- A slow reader can store an old value after a write. The TTL limits how long it stays (Q-034).
- Write order is kept per key only, so write-behind is not suitable for rows with foreign keys between them (Q-039).
- Sensitive data should not be cached without an encrypting serializer (Q-069).

### Out of scope

- An in-process (L1) cache layer (Q-076).
- Tag-based invalidation (Q-076).
- Preloading the cache at startup (Q-076).
- Cross-instance change notifications (Q-076).
- Cross-region replication (Q-076).
- Batch reads and writes (Q-019).
- Taking part in the caller's database transaction (Q-031).
- Redis Sentinel and Redis Cluster (Q-010).
- Encrypting cached values (Q-069).
- Replaying dead-letter writes (Q-085).
- Publishing the package for now (Q-086).

### Glossary

| Term | Meaning |
|------|---------|
| Policy | The read strategy, write strategy or both that are registered for a value type. |
| Loader | Application code, registered with the library, that reads a value from the database. |
| Writer | Application code, registered with the library, that writes a value to the database. |
| Cache store | The cache behind the library's public store interface. Redis is the included implementation. |
| Miss | The cache has no usable entry for a key. |
| Not found | A read outcome meaning that no value exists to return. |
| TTL | How long an entry can stay in the cache. |
| Refresh threshold | The share of an entry's TTL after which a read starts a background reload. |
| Queued write | A write-behind value waiting to be written to the database. |
| Flush | Writing queued values to the database. |
| Coalescing | Keeping only the latest queued value for a key. |
| Dead-letter store | Where a queued write goes after its retries run out. |
