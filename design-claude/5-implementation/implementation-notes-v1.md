# Library Implementation Notes

| Field       | Value                                                                                                                                         |
|-------------|-----------------------------------------------------------------------------------------------------------------------------------------------|
| Stage       | 5 – Implementation                                                                                                                            |
| Version     | v1                                                                                                                                            |
| Date        | 2026-11-02                                                                                                                                    |
| Status      | Approved                                                                                                                                      |
| Approved on | 2026-11-02                                                                                                                                    |
| Based on    | analysis-v3.md (Approved), configuration-v4.md (Approved), api-methods-v2.md (Approved), test-plan-v1.md (Approved), component-design-v2.md (Approved) |
| Supersedes  | —                                                                                                                                             |

## Changelog
- v1: First version. Records the finished implementation and its test results.

## Summary

The library is fully implemented: all six strategies, configuration and validation, the Redis store with its durable write-behind queue and dead-letter store, the hosted background service, startup checks, metrics and the consumer README. Every stub is replaced. The public API is exactly as approved in Stage 2, and none of the 217 approved tests was changed.

`dotnet pack` builds `CacheMe.1.0.0.nupkg`. It is not published, as Stage 0 decided (Q-086).

## Component to files mapping

| Component | Files |
|-----------|-------|
| Registration | `src/CacheMe/CacheMeServiceCollectionExtensions.cs`, `src/CacheMe/CacheMeBuilder.cs`, `src/CacheMe/CachePolicyBuilder.cs` |
| Policy data | `src/CacheMe/Internal/CachePolicy.cs`, `src/CacheMe/Internal/CachePolicyRegistry.cs` |
| Options validator | `src/CacheMe/Internal/CacheMeOptionsValidator.cs` |
| `Cache<T>` | `src/CacheMe/Cache.cs`, `src/CacheMe/Internal/KeyedLoader.cs` |
| JSON serializer | `src/CacheMe/Internal/JsonCacheSerializer.cs` |
| Redis store | `src/CacheMe/Internal/Redis/RedisCacheStore.cs`, `src/CacheMe/Internal/Redis/RedisCacheEntryFormat.cs`, `src/CacheMe/Internal/Redis/RedisScripts.cs` |
| Background work | `src/CacheMe/Internal/BackgroundWork.cs` |
| Write-behind processor | `src/CacheMe/Internal/WriteBehindProcessor.cs` |
| Write-behind service | `src/CacheMe/Internal/WriteBehindService.cs` |
| Redis startup check | `src/CacheMe/Internal/Redis/RedisStartupCheck.cs` |
| Metrics | `src/CacheMe/Internal/CacheMetrics.cs` |
| Models and options | `src/CacheMe/Models/`, `src/CacheMe/Options/`: the shapes are unchanged from Stage 2 |
| Consumer README | `src/CacheMe/README.md`, included in the package |

## Final test results

| Total | Passed | Failed | Skipped |
|-------|--------|--------|---------|
| 228   | 228    | 0      | 0       |

The 228 are the 217 approved tests plus the 11 added below. The full suite passed three runs in a row, including 35 tests against a real Redis 7.4 container. `dotnet build` has 0 warnings and 0 errors, with warnings treated as errors. A search of the source and test files finds no `NotImplementedException` or `TODO`.

## Tests added in this stage

| Test name | Internal component | Why |
|-----------|--------------------|-----|
| `DecodeEntry_EncodedEntry_RoundTrips` | `RedisCacheEntryFormat` | The entry format stores binary values and times. |
| `DecodeEntry_OtherData_ReturnsNull` | `RedisCacheEntryFormat` | Other data must count as unreadable (FR-016). |
| `DecodeEntry_ShorterThanHeader_ReturnsNull` | `RedisCacheEntryFormat` | Short data must not throw. |
| `DecodeWrite_ValueTypeAndValueContainSeparator_RoundTrips` | `RedisCacheEntryFormat` | The separator can appear in a type name or a value. |
| `Milliseconds_LessThanOneMillisecond_ReturnsOne` | `RedisCacheEntryFormat` | Redis rejects a zero expiry. |
| `StartAsync_SafeSettings_LogsNoWarning` | `RedisStartupCheck` | Safe settings must not raise false alarms. |
| `StartAsync_AllKeysEviction_WarnsAboutEviction` | `RedisStartupCheck` | The approved eviction test checks only log levels, and would also pass on the AOF warning. |
| `StartAsync_VolatileEviction_LogsNoWarning` | `RedisStartupCheck` | Stage 4 says `volatile-*` policies are safe. |
| `StartAsync_AofOff_WarnsAboutPersistence` | `RedisStartupCheck` | Checks the persistence warning separately. |
| `StartAsync_NoWriteBehindAndUnsafeSettings_LogsNoWarning` | `RedisStartupCheck` | The queue check applies only to write-behind. |
| `StartAsync_SuppliedConnectionWithoutAdminMode_StillChecksSettings` | `RedisStartupCheck` | Covers the deviation below. |

## Deviations from the design

**One, which approving this version accepts:**

- **How the startup check reads `INFO`.** Stage 4 said the check "uses `INFO`, which does not need admin rights". That was wrong for this client: StackExchange.Redis refuses `IServer.InfoAsync` and `ExecuteAsync("INFO", ...)` unless the connection is in admin mode. The first test run found this. The check now runs `INFO` from a read-only Lua script, which needs no admin mode, including on a connection the application supplies. Turning on admin mode would have allowed more than this check needs, and would not have helped with supplied connections. The behaviour matches the design: it warns when settings are readable and unsafe, and skips the check when they are not readable.

Nothing else differs from the approved design.

## Known limitations

**Not measured**

- **NFR-001 has not been measured.** The target of under 1 ms overhead at 1,000 operations per second is unverified, because the workflow has no benchmark. Treat it as a goal, not a result.
- **Thread-safety has limited evidence (NFR-005).** Tests cover concurrent reads and misses and two instances sharing a queue, but no test can prove every interleaving.
- **Three clean runs is a small sample** for tests with background timing. A loaded CI machine could expose flakiness that these runs did not.

**Behaviour to plan for** (the first two are also in the consumer README)

- **A write-behind write can run more than once after a crash** (Q-001 in Stage 4), so writers must be safe to repeat.
- **Write-behind depends on Redis keeping its data.** A default Redis has AOF off, so applications using write-behind get a startup warning until it is turned on.
- **Fixed values with no setting:** claims last 5 minutes, a flush runs up to 8 writes at once, and the loop polls every 250 ms. A writer running longer than 5 minutes can run twice.
- **A write interrupted at shutdown stays claimed** for up to 5 minutes before another instance can retry it.
- **Each instance refreshes a hot key separately,** so N instances can each reload the same key once past the threshold.
- **The queue depth metric is only as fresh as the last flush,** and stays at zero for applications without write-behind.

**Environment**

- **A cluster-mode endpoint is not detected.** Stage 0 supports single endpoints, and the connection string check can only see the number of endpoints. A managed Redis running in cluster mode behind one endpoint would fail on the multi-key scripts.
- **A managed Redis that restricts Lua scripts** would break write-behind and every entry read, because they use scripts. I have tested only against Redis 7.4 in a container.
- **Requeued writes use the system clock.** `CompleteAsync` takes no time parameter in the approved API, so when a newer value replaces a write, the requeue time comes from the application's system clock rather than `TimeProvider`. This has no effect outside tests with a fake clock.

**Documentation outside this stage**

- **The root `README.md` still shows the old write-through order** (cache first). The library writes the database first, as Q-029 in Stage 0 decided. The consumer README is correct.

## Assumptions

- The consumer README is at `src/CacheMe/README.md` and ships in the package, which adds `PackageReadmeFile` to the project file. It is written for developers using the library.
- Package versions are the latest stable when added: `StackExchange.Redis` 3.4.29 and `Microsoft.Extensions.*` 10.0.14.

## Open Questions

None.

## Decisions

None. This stage raised no questions.
