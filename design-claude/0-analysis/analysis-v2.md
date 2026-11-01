# Library Analysis

| Field       | Value                 |
|-------------|-----------------------|
| Stage       | 0 – Analysis          |
| Version     | v2                    |
| Date        | 2026-11-01            |
| Status      | Awaiting answers      |
| Approved on | —                     |
| Based on    | lib-requirements.md   |
| Supersedes  | v1                    |

## Changelog
- v2: Recorded the answer "select suggested". Moved 70 questions to Decisions and closed 5 [Minor] questions under Assumptions. Kept Q-010 and Q-035 open because v1 offered no suggestion for them, and added one now. Added Q-078 to Q-088, which are conflicts and gaps exposed by the combined answers, plus the package details promised in Q-053.

## Requirements as understood

The library lets an application cache values in Redis, or another store through a public interface, without writing the caching logic itself. It implements the six strategies in the root `README.md`:

- **Read:** cache-aside, read-through, refresh-ahead (as an add-on to read-through).
- **Write:** write-through, write-behind, write-around.

The caller is the repository layer of an application built with hexagonal architecture, running as many instances. Repositories call an async API. Each value type's strategy is set at registration, not on each call as the requirements first said (Q-003). The library reaches the database through registered loaders and writers, each resolved in a new DI scope.

A read returns a value, not found, or an error. What a successful write means depends on the strategy (Q-028).

## Assumptions

**Closed [Minor] questions, with your agreement ("select suggested")**

- Q-008: I write the concrete examples into the brief, and you review them.
- Q-019: Single values only in the first version, with no batch operations.
- Q-026: The refresh threshold is configurable and defaults to 75% of TTL.
- Q-075: A consumer README is produced at the end.
- Q-077: There are no outside constraints from company policy, approved packages or licensing.

**Carried from v1**

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

**Carried over from v1**

### Q-010 [Important] Redis deployment to support
**Why it matters:** Redis Cluster restricts multi-key operations to one hash slot, which affects batching and durable queues.
**Options:** a) Standalone or managed single endpoint only  b) Also Sentinel  c) Also Cluster
**Suggested:** a — it is the simplest. Choose b or c if you already run Redis that way, because the durable queue (Q-036) must be designed for it.
**Answer:**
>

### Q-035 [Important] Maximum acceptable staleness
**Why it matters:** other systems or manual fixes can change the database without the library knowing. Only the TTL corrects those changes, so this sets the upper limit for TTLs.
**Options:** Give a number, for example 5 minutes, or say that it varies by value type.
**Suggested:** 5 minutes for all value types, adjustable per type — a placeholder. Only you know how stale your data can safely be.
**Answer:**
>

**Follow-ups: conflicts between accepted answers**

### Q-078 (follow-up to Q-001) [Blocking] Storing a value after a cache-aside miss
**Why it matters:** under Q-001 the caller handles the miss and stores the loaded value. Under Q-005 cache-aside can be paired with write-through. If that store goes through the write strategy, a value just read from the database gets written straight back to it.
**Options:** a) Two operations: a cache-only store that never triggers a write strategy, and a write that follows it  b) One write operation, and cache-aside cannot be paired with a write strategy (this reverses Q-005)
**Suggested:** a — it keeps Q-005 and makes the difference explicit in the API.
**Answer:**
>

### Q-079 (follow-up to Q-003) [Important] What a strategy is registered against
**Why it matters:** Q-003 said "per value type or named policy" without choosing, and this decides how a repository's call finds its strategy.
**Options:** a) One policy per value type  b) Named policies only  c) Per value type, with optional named variants
**Suggested:** a — nothing in the requirements needs one type cached in two ways, and calls stay simple.
**Answer:**
>

### Q-080 (follow-up to Q-003) [Important] Read-only or write-only policies
**Why it matters:** some value types may only ever be read through the library. A policy could also be used for an operation it has no strategy for.
**Options:** a) A policy sets a read strategy, a write strategy or both; calling the missing side is a programmer error (Q-060)  b) Every policy must set both
**Suggested:** a — it does not force a write strategy onto read-only data.
**Answer:**
>

### Q-081 (follow-up to Q-041) [Important] Refresh-ahead while a write is queued
**Why it matters:** the README recommends combining read-through with write-behind. A background reload can load the older row and overwrite the newer cached value before the queued write reaches the database. The caller's write then appears to be lost until the entry is next reloaded.
**Options:** a) Skip the refresh while the key has a queued write  b) Let the refresh run and accept the overwrite
**Suggested:** a — it keeps the read-your-own-write behaviour chosen in Q-041.
**Answer:**
>

### Q-082 (follow-up to Q-036) [Important] Redis settings the durable queue relies on
**Why it matters:** a queue in Redis is durable only if Redis keeps it. Without persistence a Redis restart can lose queued writes, and an eviction policy such as `allkeys-lru` can remove them under memory pressure. Managed Redis services may also block reading these settings.
**Options:** a) Document the required settings and log a startup warning when settings are readable and unsafe  b) Refuse to start when settings are unsafe or cannot be read  c) Document only
**Suggested:** a — it warns you without blocking services where settings cannot be read.
**Answer:**
>

### Q-083 (follow-up to Q-036) [Important] Cache unavailable during a write-behind write
**Why it matters:** the queue and the value both live in Redis, so an outage stops write-behind completely. Q-021 covers reads only.
**Options:** a) Return an error; nothing is queued  b) Fall back to writing the database directly
**Suggested:** a — b could overtake older writes still queued, which breaks per-key order (Q-039), and write latency would change without warning.
**Answer:**
>

### Q-084 (follow-up to Q-033) [Important] Write-around when the database succeeds but the cache fails
**Why it matters:** Q-030 covers write-through only. Here the entry could not be removed, so readers can get the old value until its TTL ends.
**Options:** a) Same as Q-030: report success and log a warning  b) Report failure, even though the data is saved
**Suggested:** a — consistent with Q-030, and it avoids retries of a write that already succeeded.
**Answer:**
>

### Q-085 (follow-up to Q-040) [Important] Dead-letter store
**Why it matters:** Q-040 chose a dead-letter store but not where it lives or how long it keeps writes. Without a limit it grows forever, and failed writes can hold personal data.
**Options:** a) In Redis, with configurable retention; your handler (Q-040) decides what to do with each write, and the library does not replay them  b) Handler only, with nothing stored  c) In Redis, with no expiry
**Suggested:** a — failed writes survive a handler failure, and retention limits growth.
**Answer:**
>

**Follow-ups: NuGet package details (from Q-053)**

### Q-086 [Important] Where the package is published
**Why it matters:** publishing to nuget.org is public and hard to undo, because published versions can be unlisted but not fully removed.
**Options:** a) nuget.org  b) A private feed  c) Build the package only; decide on publishing later
**Suggested:** c — nothing in the requirements needs publishing yet.
**Answer:**
>

### Q-087 [Important] Package ID
**Why it matters:** the ID is permanent once published. I have not checked whether `CacheMe` is free on nuget.org, which matters only if Q-086 is a.
**Options:** a) `CacheMe`  b) Another ID
**Suggested:** a — it matches the project.
**Answer:**
>

### Q-088 [Minor] Package metadata
**Why it matters:** these values go into the package. The author and license come from `LICENSE`, and the URL from the git remote.
**Options:** a) Authors `Nikhil P`; license `MIT`; repository `https://github.com/codefinity/cache-me`; description "Read and write caching strategies for .NET, with Redis support."  b) Different values
**Suggested:** a — taken from the repository, apart from the description.
**Answer:**
>

## Decisions

Every answer below was given in chat, as the one instruction "select suggested".

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

## Out of scope

- An in-process (L1) cache layer (Q-076).
- Tag-based invalidation (Q-076).
- Preloading the cache at startup (Q-076).
- Cross-instance change notifications (Q-076).
- Cross-region replication (Q-076).
- Batch reads and writes (Q-019).
- Taking part in the caller's database transaction (Q-031).
