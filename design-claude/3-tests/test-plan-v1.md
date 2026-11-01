# Library Test Plan

| Field       | Value                                                                                    |
|-------------|------------------------------------------------------------------------------------------|
| Stage       | 3 – Tests                                                                                |
| Version     | v1                                                                                       |
| Date        | 2026-11-01                                                                               |
| Status      | Approved                                                                                 |
| Approved on | 2026-11-01                                                                               |
| Based on    | analysis-v3.md (Approved), configuration-v4.md (Approved), api-methods-v2.md (Approved)  |
| Supersedes  | —                                                                                        |

## Changelog
- v1: First version. Plans and records the tests that specify the approved behaviour before it is implemented.

## Test project
- **Path:** `tests/CacheMe.Tests`
- **Packages:**
    - `xunit.v3` 4.0.2
    - `xunit.runner.visualstudio` 4.0.1
    - `Microsoft.NET.Test.Sdk` 18.11.0
    - `Moq` 4.21.0
    - `Testcontainers.Redis` 4.16.0
    - `Microsoft.Extensions.Hosting` 10.0.14
- **Run everything:** `dotnet test --solution CacheMe.slnx` (needs Docker running)
- **Run without Redis:** `dotnet test --solution CacheMe.slnx --filter-not-trait "Category=Integration"`

The `--solution` option is needed because `global.json` opts in to the Microsoft Testing Platform (see Assumptions).

## Mocking approach

Tests create the real `Cache<T>` and `WriteBehindProcessor` through their internal constructors, using the shared `CacheHarness` in `Support/TestSupport.cs`.

| Seam | Mocked with | Typical setup |
|------|-------------|---------------|
| `ICacheStore`, `IWriteBehindStore` | `Mock<IWriteBehindStore>` | Returns an entry or null; throws to simulate an outage; `TryEnqueueAsync` returns true by default |
| `ICacheSerializer` | `Mock<ICacheSerializer>` | Returns fixed bytes and the test product; throws to simulate unreadable data |
| `ICacheLoader<T>`, `ICacheWriter<T>` | `Mock<ICacheLoader<Product>>`, `Mock<ICacheWriter<Product>>` | Registered in a real scoped provider, so each use gets a new scope |
| `IDeadLetterHandler` | `Mock<IDeadLetterHandler>` | Registered in the same provider; throws to test handler failures |
| `IServiceScopeFactory` | Not mocked: a real `ServiceProvider` | Holds the mocked loader, writer and handler |
| `IOptions<CacheMeOptions>` | Not mocked: `Options.Create` | Prefix `shop:` and a 5-minute default TTL |
| `ILogger<T>` | `Mock<ILogger<T>>` | `IsEnabled` returns true; tests verify the log level |
| `TimeProvider` | Not mocked: a small `TestClock` | Tests set the time by hand |
| `IBackgroundWork` | `Mock<IBackgroundWork>` | Captures the work, so a test can run a refresh directly |
| `ICacheMetrics` | `Mock<ICacheMetrics>` | Tests verify what was recorded |
| `IWriteBehindStatus` | `Mock<IWriteBehindStatus>` | `IsRunning` returns true by default |

The Redis container tests use no mocks. They run the whole library against a real Redis 7.4 container through the .NET generic host, with recording loaders, writers and handlers. They cover what Moq cannot prove: TTLs, durable queues, and instances sharing a queue.

## Traceability matrix
| Requirement | Tests |
|-------------|-------|
| FR-001 | `AddCacheMe_TypeRegistered_ResolvesCache`, `AddCacheMe_TypeNotRegistered_FailsWhenResolved`, `Options_PolicyKeyHasNoRegisteredType_FailsValidation`, `ForType_SameTypeTwice_ThrowsInvalidOperationException`, `ForType_SameSettingsKeyTwice_ThrowsInvalidOperationException`, `ForType_NoStrategy_ThrowsInvalidOperationException`, `CacheAside_ReadStrategyAlreadySet_ThrowsInvalidOperationException`, `ReadThrough_ReadStrategyAlreadySet_ThrowsInvalidOperationException`, `WriteBehind_WriteStrategyAlreadySet_ThrowsInvalidOperationException`, `WriteAround_WriteStrategyAlreadySet_ThrowsInvalidOperationException`, `WriteThrough_WriteStrategyAlreadySet_ThrowsInvalidOperationException` |
| FR-002 | `WriteAsync_CacheAsideWithWriteThrough_FollowsWriteStrategy`, `ForType_ReadAndWriteStrategy_Registers` |
| FR-003 | `GetAsync_PolicyHasNoReadStrategy_ThrowsInvalidOperationException`, `WriteAsync_PolicyHasNoWriteStrategy_ThrowsInvalidOperationException` |
| FR-004 | `GetAsync_CacheAsideHit_ReturnsFoundWithValue`, `GetAsync_CacheAsideMiss_ReturnsNotFound`, `Found_AnyValue_SetsStatusAndValue`, `NotFound_NoArguments_SetsStatusOnly`, `ReadFailed_AnyError_SetsStatusAndError` |
| FR-005 | `GetAsync_CacheAsideHit_ReturnsFoundWithValue`, `GetAsync_CacheAsideMiss_ReturnsNotFound`, `GetAsync_CacheAsideMiss_DoesNotCallLoader`, `StoreInCacheAsync_CacheAside_StoresValueWithPrefixedKey`, `StoreInCacheAsync_CacheAside_ValueRoundTrips` |
| FR-006 | `StoreInCacheAsync_ReadStrategyIsNotCacheAside_ThrowsInvalidOperationException`, `StoreInCacheAsync_CacheAside_StoresValueWithPrefixedKey`, `StoreInCacheAsync_PolicyAlsoHasWriteThrough_DoesNotCallWriter`, `StoreInCacheAsync_CacheAside_ValueRoundTrips` |
| FR-007 | `GetAsync_ReadThroughHit_DoesNotCallLoader`, `GetAsync_ReadThroughMiss_ReturnsLoadedValue`, `GetAsync_ReadThroughMiss_StoresValueWithDefaultTtl`, `GetAsync_ReadThroughMiss_StoresEntryUnderPrefixedKeyWithTtl`, `GetAsync_ReadThroughSecondRead_DoesNotCallLoaderAgain` |
| FR-008 | `GetAsync_EntryBeforeThreshold_DoesNotStartRefresh`, `GetAsync_EntryExactlyAtThreshold_DoesNotStartRefresh`, `GetAsync_EntryPastThreshold_ReturnsCurrentValueAndStartsRefresh`, `GetAsync_RefreshRuns_LoadsAndStoresNewValue`, `GetAsync_ReadThroughWithoutRefreshAhead_DoesNotStartRefresh`, `StoreInCacheAsync_AnyValue_PassesCurrentTimeAsCreatedAt`, `Options_ThresholdSetForTypeWithoutRefreshAhead_FailsValidation`, `RefreshAhead_NoReadStrategy_ThrowsInvalidOperationException`, `RefreshAhead_WithCacheAside_ThrowsInvalidOperationException`, `GetAsync_EntryPastThreshold_ReloadsInBackground` |
| FR-009 | `GetAsync_EntryExactlyAtThreshold_DoesNotStartRefresh`, `GetAsync_GeneralThresholdChanged_UsesIt`, `GetAsync_PolicyThresholdSet_OverridesGeneralThreshold`, `Options_SettingOutOfRange_FailsValidation`, `Options_SettingAtBoundary_PassesValidation`, `CacheMeOptions_NoSettings_HasApprovedDefaults` |
| FR-010 | `GetAsync_KeyHasQueuedWrite_SkipsRefresh` |
| FR-011 | `GetAsync_RefreshLoaderThrows_KeepsCurrentValueAndLogsWarning`, `GetAsync_ReadAfterFailedRefresh_TriesAgain` |
| FR-012 | `GetAsync_CacheAsideStoreFails_ReturnsStoreUnavailable`, `GetAsync_ReadThroughStoreFails_FallsBackToLoaderAndLogsWarning`, `GetAsync_ReadThroughStoreFailsOnlyOnStore_ReturnsFoundAndLogsWarning`, `GetAsync_ReadThroughStoreAndLoaderFail_ReturnsLoaderFailed`, `GetAsync_RedisUnreachableReadThrough_FallsBackAndLogsWarning`, `GetAsync_RedisUnreachableCacheAside_ReturnsStoreUnavailable`, `StartAsync_RedisUnreachable_DoesNotStopApplicationAndLogsWarning` |
| FR-013 | `GetAsync_ReadThroughLoaderThrows_ReturnsLoaderFailedWithoutRetrying`, `GetAsync_ReadThroughStoreAndLoaderFail_ReturnsLoaderFailed` |
| FR-014 | `GetAsync_ReadThroughLoaderReturnsNull_ReturnsNotFoundAndCachesNothing` |
| FR-015 | `GetAsync_ConcurrentMissesOnOneKey_CallLoaderOnce`, `GetAsync_OneCallerCancelsSharedLoad_OtherCallerStillGetsValue`, `GetAsync_ReadsWhileRefreshRunning_StartOneRefresh` |
| FR-016 | `GetAsync_CacheAsideUnreadableEntry_ReturnsNotFoundAndLogsWarning`, `GetAsync_ReadThroughUnreadableEntry_LoadsFromLoader`, `GetAsync_EntryCannotBeRead_ReturnsNotFound` |
| FR-017 | `WriteAsync_WriteThrough_WritesDatabaseBeforeCache`, `WriteAsync_WriteThrough_WritesDatabaseAndCache` |
| FR-018 | `WriteAsync_WriteThroughWriterThrows_ReturnsWriterFailedAndLeavesCache` |
| FR-019 | `WriteAsync_WriteThroughCacheFails_ReportsSuccessLogsWarningAndRemovesOldEntry`, `WriteAsync_WriteThroughCacheAndRemoveFail_StillReportsSuccess` |
| FR-020 | `WriteAsync_WriteBehind_QueuesWriteAndReportsSuccess`, `WriteAsync_WriteBehind_DoesNotCallWriter`, `WriteAsync_WriteBehindServiceNotRunning_ThrowsInvalidOperationException`, `FlushOnceAsync_ClaimedWrite_CallsWriterWithKeyWithoutPrefix`, `WriteAsync_WriteBehind_WritesDatabaseInBackground`, `WriteAsync_NoHostStarted_ThrowsInvalidOperationException` |
| FR-021 | `WriteAsync_WriteBehindStoreFails_ReturnsStoreUnavailableAndDoesNotCallWriter` |
| FR-022 | `FlushOnceAsync_NothingDue_ReturnsZero`, `FlushOnceAsync_FewerThanBatchWaiting_ClaimsOnlyWritesPastMaxDelay`, `FlushOnceAsync_FullBatchWaiting_ClaimsWithoutWaitingForMaxDelay`, `FlushOnceAsync_SeveralWrites_CallsWriterOncePerWrite`, `Options_SettingOutOfRange_FailsValidation`, `CacheMeOptions_NoSettings_HasApprovedDefaults`, `WriteAsync_WriteBehind_WritesDatabaseInBackground`, `WriteAsync_FullBatchWaiting_FlushesBeforeMaxDelay` |
| FR-023 | `FlushOnceAsync_WriterSucceeds_CompletesWrite`, `WriteAsync_TwoWritesToOneKeyBeforeFlush_WritesLatestOnce`, `WriteAsync_WriteArrivesWhileFlushing_NewestValueEndsUpLast`, `WriteAsync_QueueFullAndKeyAlreadyQueued_ReplacesValue` |
| FR-024 | `WriteAsync_WriteArrivesWhileFlushing_NewestValueEndsUpLast` |
| FR-025 | `FlushOnceAsync_OneWriteFails_StillProcessesOthers`, `FlushOnceAsync_FirstTryFails_RetriesAfterInitialDelay`, `FlushOnceAsync_ThirdTryFails_DoublesDelayTwice`, `FlushOnceAsync_DelayWouldPassMaxDelay_UsesMaxDelay`, `FlushOnceAsync_FinalTryFails_MovesToDeadLetterWithRetention`, `FlushOnceAsync_FinalTryFails_LogsError`, `FlushOnceAsync_FinalTryFails_NotifiesHandler`, `FlushOnceAsync_QueuedValueCannotBeRead_MovesToDeadLetterWithoutRetrying`, `FlushOnceAsync_QueuedValueTypeNotRegistered_MovesToDeadLetter`, `Options_SettingOutOfRange_FailsValidation`, `UseDeadLetterHandler_AnyHandler_RegistersItAsScoped`, `CacheMeOptions_NoSettings_HasApprovedDefaults`, `WriteAsync_WriterKeepsFailing_MovesToDeadLetterStoreAndNotifiesHandler` |
| FR-026 | `FlushOnceAsync_FinalTryFails_MovesToDeadLetterWithRetention`, `FlushOnceAsync_HandlerThrows_LogsAndKeepsDeadLetterWrite`, `Options_SettingOutOfRange_FailsValidation`, `AddCacheMe_RedisStore_RegistersDeadLetterStore`, `AddCacheMe_PlainStore_DoesNotRegisterDeadLetterStore`, `CacheMeOptions_NoSettings_HasApprovedDefaults`, `WriteAsync_WriterKeepsFailing_MovesToDeadLetterStoreAndNotifiesHandler`, `DeleteAsync_StoredFailedWrite_RemovesIt` |
| FR-027 | `FlushOnceAsync_WriterSucceeds_CompletesWrite`, `WriteAsync_WriteQueued_EntryHasNoExpiryUntilCompleted` |
| FR-028 | `WriteAsync_WriteBehindServiceNotRunning_ThrowsInvalidOperationException`, `FlushAllAsync_WritesQueued_FlushesUntilEmpty`, `FlushAllAsync_AnyWrite_DoesNotWaitForMaxFlushDelay`, `FlushAllAsync_ShutdownTimeoutPasses_Stops`, `Options_SettingOutOfRange_FailsValidation`, `CacheMeOptions_NoSettings_HasApprovedDefaults`, `StopAsync_WritesQueued_FlushesThem`, `WriteAsync_NoHostStarted_ThrowsInvalidOperationException` |
| FR-029 | `WriteAsync_WriteBehindQueueFull_ReturnsQueueFull`, `Options_SettingOutOfRange_FailsValidation`, `CacheMeOptions_NoSettings_HasApprovedDefaults`, `WriteAsync_QueueFull_ReturnsQueueFull`, `WriteAsync_QueueFullAndKeyAlreadyQueued_ReplacesValue` |
| FR-030 | `WriteAsync_WriteAround_WritesDatabaseThenRemovesEntry`, `WriteAsync_WriteAround_StoresNothingInCache`, `WriteAsync_WriteAroundWriterThrows_ReturnsWriterFailedAndKeepsEntry`, `WriteAsync_WriteAround_RemovesExistingEntry` |
| FR-031 | `WriteAsync_WriteAroundRemoveFails_ReportsSuccessAndLogsWarning` |
| FR-032 | `RemoveAsync_EntryExists_RemovesWithPrefixedKey`, `RemoveAsync_NoEntry_StillSucceeds`, `RemoveAsync_StoreSupportsWriteBehind_CancelsQueuedWrite`, `RemoveAsync_AnyPolicy_DoesNotTouchDatabase`, `RemoveAsync_EntryExists_RemovesIt`, `RemoveAsync_WriteQueued_CancelsIt` |
| FR-033 | `GetAsync_AnyKey_AddsPrefixForStore`, `GetAsync_ReadThroughMiss_PassesKeyWithoutPrefixToLoader`, `FlushOnceAsync_ClaimedWrite_CallsWriterWithKeyWithoutPrefix`, `Options_KeyPrefixInvalid_FailsValidation`, `Options_KeyPrefixOver128Characters_FailsValidation`, `GetAsync_ReadThroughMiss_StoresEntryUnderPrefixedKeyWithTtl` |
| FR-034 | `GetAsync_ReadThroughMiss_StoresValueWithDefaultTtl`, `GetAsync_ReadThroughMissWithPolicyTtl_StoresValueWithPolicyTtl`, `StoreInCacheAsync_TtlGiven_UsesIt`, `WriteAsync_WriteThroughTtlGiven_StoresWithIt`, `Options_DefaultTtlMissing_FailsValidation`, `Options_DefaultTtlOverMaxTtl_FailsValidation`, `Options_PolicyDefaultTtlOverPolicyMaxTtl_NamesPolicyInFailure`, `GetAsync_ReadThroughMiss_StoresEntryUnderPrefixedKeyWithTtl` |
| FR-035 | `UseSerializer_CustomSerializer_ReplacesDefault`, `StoreInCacheAsync_CacheAside_ValueRoundTrips` |
| FR-036 | `Options_UseRedisWithoutConnectionString_FailsValidation`, `UseRedis_ExistingConnection_DoesNotNeedConnectionString`, `UseStore_CustomStore_DoesNotNeedRedisSettings`, `AddCacheMe_TwoStores_ThrowsInvalidOperationException`, `AddCacheMe_NoStore_ThrowsInvalidOperationException`, `AddCacheMe_WriteBehindWithPlainStore_ThrowsInvalidOperationException`, `UseRedis_ExistingConnection_Works` |
| FR-037 | `GetAsync_NullKey_ThrowsArgumentNullException`, `GetAsync_EmptyKey_ThrowsArgumentException`, `GetAsync_KeyOver1024Characters_ThrowsArgumentException`, `GetAsync_KeyOf1024Characters_IsAccepted`, `StoreInCacheAsync_NullKey_ThrowsArgumentNullException`, `StoreInCacheAsync_NullValue_ThrowsArgumentNullException`, `StoreInCacheAsync_EmptyKey_ThrowsArgumentException`, `StoreInCacheAsync_KeyOver1024Characters_ThrowsArgumentException`, `StoreInCacheAsync_NonPositiveTtl_ThrowsArgumentOutOfRangeException`, `WriteAsync_NullKey_ThrowsArgumentNullException`, `WriteAsync_NullValue_ThrowsArgumentNullException`, `WriteAsync_EmptyKey_ThrowsArgumentException`, `WriteAsync_KeyOver1024Characters_ThrowsArgumentException`, `WriteAsync_NonPositiveTtl_ThrowsArgumentOutOfRangeException`, `RemoveAsync_NullKey_ThrowsArgumentNullException`, `RemoveAsync_EmptyKey_ThrowsArgumentException`, `RemoveAsync_KeyOver1024Characters_ThrowsArgumentException` |
| FR-038 | `StoreInCacheAsync_ValueOverMaxValueBytes_ReturnsValueTooLarge`, `StoreInCacheAsync_ValueExactlyAtMaxValueBytes_IsStored`, `WriteAsync_WriteThroughValueTooLarge_ReturnsErrorBeforeWritingDatabase`, `WriteAsync_WriteBehindValueTooLarge_ReturnsErrorAndQueuesNothing`, `WriteAsync_WriteAroundValueOverMaxValueBytes_IsStillWritten`, `Options_SettingOutOfRange_FailsValidation`, `Options_SettingAtBoundary_PassesValidation` |
| NFR-001 | See Gaps |
| NFR-002 | `StoreInCacheAsync_TtlOverMaxTtl_ThrowsArgumentOutOfRangeException`, `StoreInCacheAsync_TtlEqualsMaxTtl_IsAccepted`, `StoreInCacheAsync_PolicyRaisesMaxTtl_AcceptsLongerTtl`, `WriteAsync_TtlOverMaxTtl_ThrowsArgumentOutOfRangeException`, `Options_DefaultTtlOverMaxTtl_FailsValidation`, `Options_PolicyDefaultTtlOverPolicyMaxTtl_NamesPolicyInFailure`, `Options_PolicyRaisesMaxTtl_PassesValidation`, `CacheMeOptions_NoSettings_HasApprovedDefaults` |
| NFR-003 | `Options_SettingOutOfRange_FailsValidation`, `CacheMeOptions_NoSettings_HasApprovedDefaults`, `GetAsync_RedisUnreachable_FailsWithinOperationTimeout` |
| NFR-004 | `CacheMeOptions_NoSettings_HasApprovedDefaults` |
| NFR-005 | `GetAsync_ManyConcurrentReaders_AllGetValue` |
| NFR-006 | `WriteAsync_TwoInstancesShareQueue_EachWriteHappensOnce` |
| NFR-007 | `WriteAsync_InstanceCrashes_AnotherInstanceFinishesWrite`, `StartAsync_EvictionPolicyCanRemoveQueue_LogsWarning` |
| NFR-008 | `GetAsync_Hit_RecordsHit`, `GetAsync_Miss_RecordsMiss`, `GetAsync_StoreFails_RecordsError`, `WriteAsync_WriterThrows_RecordsError`, `FlushOnceAsync_WriterThrows_RecordsError`, `CacheMeMetrics_Names_MatchApprovedContract` |
| TD-004 | `AddCacheMe_JsonConfiguration_BindsAllSettings`, `Configure_SettingAlsoInJson_CodeWins`, `AddCacheMe_CodeOnly_UsesConfigureAndConnectionString`, `Options_SeveralInvalidSettings_ReportsAllTogether`, `Options_UnknownKey_ThrowsInvalidOperationExceptionNamingIt`, `AddCacheMe_ValidJson_PassesValidation`, `CacheMeOptions_PolicyKeys_AreCaseInsensitive` |
| TD-005 | `GetAsync_CancelledToken_ThrowsOperationCanceledException`, `GetAsync_OneCallerCancelsSharedLoad_OtherCallerStillGetsValue`, `StoreInCacheAsync_CancelledToken_ThrowsOperationCanceledException`, `WriteAsync_CancelledToken_ThrowsOperationCanceledException`, `RemoveAsync_CancelledToken_ThrowsOperationCanceledException` |
| TD-006 | `AddCacheMe_TypeRegistered_RegistersCacheAsSingleton`, `ForType_LoaderNotRegistered_RegistersItAsScoped`, `ForType_LoaderAlreadyRegistered_KeepsRegistration` |
| TD-007 | `StoreInCacheAsync_SerializerThrows_ReturnsSerializationFailed`, `StoreInCacheAsync_StoreFails_ReturnsStoreUnavailable`, `WriteAsync_WriteThroughSerializerThrows_ReturnsErrorBeforeWritingDatabase`, `RemoveAsync_StoreFails_ReturnsStoreUnavailable`, `Success_NoArguments_SetsSucceeded`, `WriteFailed_AnyError_SetsError`, `CacheErrorCtor_AllArguments_StoresThem` |
| TD-008 | `CacheMeMetrics_Names_MatchApprovedContract` |
| TD-009 | `Options_ConnectionStringListsTwoEndpoints_FailsValidation`, `UseRedis_ExistingConnection_DoesNotNeedConnectionString`, `AddCacheMe_NoClockRegistered_UsesSystemClock`, `AddCacheMe_ClockAlreadyRegistered_KeepsIt`, `UseRedis_ExistingConnection_Works` |
| TD-011 | `Options_ConnectionStringInvalid_DoesNotPutItInMessage` |

## Test catalogue

There are 198 test methods. Theories run once per case, which makes 217 test cases. Scenario and expected result come from each test's `Method_Scenario_ExpectedResult` name.

### `Cache<T>.GetAsync`
| Test name | Scenario | Mocks set up | Expected result |
|-----------|----------|--------------|-----------------|
| `GetAsync_NullKey_ThrowsArgumentNullException` | null key | Harness defaults | throws argument null exception |
| `GetAsync_EmptyKey_ThrowsArgumentException` | empty key | Harness defaults | throws argument exception |
| `GetAsync_KeyOver1024Characters_ThrowsArgumentException` | key over1024 characters | Harness defaults | throws argument exception |
| `GetAsync_KeyOf1024Characters_IsAccepted` | key of1024 characters | Harness defaults | is accepted |
| `GetAsync_PolicyHasNoReadStrategy_ThrowsInvalidOperationException` | policy has no read strategy | Harness defaults | throws invalid operation exception |
| `GetAsync_CancelledToken_ThrowsOperationCanceledException` | cancelled token | Harness defaults | throws operation canceled exception |
| `GetAsync_AnyKey_AddsPrefixForStore` | any key | Harness defaults | adds prefix for store |
| `GetAsync_CacheAsideHit_ReturnsFoundWithValue` | cache aside hit | `Store.GetAsync` | returns found with value |
| `GetAsync_CacheAsideMiss_ReturnsNotFound` | cache aside miss | `Store.GetAsync` | returns not found |
| `GetAsync_CacheAsideMiss_DoesNotCallLoader` | cache aside miss | `Store.GetAsync` | does not call loader |
| `GetAsync_CacheAsideStoreFails_ReturnsStoreUnavailable` | cache aside store fails | `Store.GetAsync` throws | returns store unavailable |
| `GetAsync_CacheAsideUnreadableEntry_ReturnsNotFoundAndLogsWarning` | cache aside unreadable entry | `Store.GetAsync`, `Serializer` | returns not found and logs warning |
| `GetAsync_ReadThroughHit_DoesNotCallLoader` | read through hit | `Store.GetAsync` | does not call loader |
| `GetAsync_ReadThroughMiss_ReturnsLoadedValue` | read through miss | `Store.GetAsync`, `Loader.LoadAsync` | returns loaded value |
| `GetAsync_ReadThroughMiss_StoresValueWithDefaultTtl` | read through miss | `Store.GetAsync`, `Loader.LoadAsync` | stores value with default ttl |
| `GetAsync_ReadThroughMissWithPolicyTtl_StoresValueWithPolicyTtl` | read through miss with policy ttl | `Store.GetAsync`, `Loader.LoadAsync`, Settings changed | stores value with policy ttl |
| `GetAsync_ReadThroughMiss_PassesKeyWithoutPrefixToLoader` | read through miss | `Store.GetAsync`, `Loader.LoadAsync` | passes key without prefix to loader |
| `GetAsync_ReadThroughLoaderReturnsNull_ReturnsNotFoundAndCachesNothing` | read through loader returns null | `Store.GetAsync`, `Loader.LoadAsync` | returns not found and caches nothing |
| `GetAsync_ReadThroughLoaderThrows_ReturnsLoaderFailedWithoutRetrying` | read through loader throws | `Store.GetAsync`, `Loader.LoadAsync` | returns loader failed without retrying |
| `GetAsync_ReadThroughStoreFails_FallsBackToLoaderAndLogsWarning` | read through store fails | `Store.GetAsync` throws, `Loader.LoadAsync` | falls back to loader and logs warning |
| `GetAsync_ReadThroughStoreFailsOnlyOnStore_ReturnsFoundAndLogsWarning` | read through store fails only on store | `Store.GetAsync`, `Loader.LoadAsync`, `Store.SetAsync` | returns found and logs warning |
| `GetAsync_ReadThroughStoreAndLoaderFail_ReturnsLoaderFailed` | read through store and loader fail | `Store.GetAsync` throws, `Loader.LoadAsync` | returns loader failed |
| `GetAsync_ReadThroughUnreadableEntry_LoadsFromLoader` | read through unreadable entry | `Store.GetAsync`, `Loader.LoadAsync`, `Serializer` | loads from loader |
| `GetAsync_ConcurrentMissesOnOneKey_CallLoaderOnce` | concurrent misses on one key | `Store.GetAsync`, `Loader.LoadAsync` | call loader once |
| `GetAsync_OneCallerCancelsSharedLoad_OtherCallerStillGetsValue` | one caller cancels shared load | `Store.GetAsync`, `Loader.LoadAsync` | other caller still gets value |
| `GetAsync_EntryBeforeThreshold_DoesNotStartRefresh` | entry before threshold | `Store.GetAsync` | does not start refresh |
| `GetAsync_EntryExactlyAtThreshold_DoesNotStartRefresh` | entry exactly at threshold | `Store.GetAsync` | does not start refresh |
| `GetAsync_EntryPastThreshold_ReturnsCurrentValueAndStartsRefresh` | entry past threshold | `Store.GetAsync` | returns current value and starts refresh |
| `GetAsync_RefreshRuns_LoadsAndStoresNewValue` | refresh runs | `Store.GetAsync`, `Loader.LoadAsync`, `IBackgroundWork.Run` captures the work | loads and stores new value |
| `GetAsync_ReadThroughWithoutRefreshAhead_DoesNotStartRefresh` | read through without refresh ahead | `Store.GetAsync` | does not start refresh |
| `GetAsync_GeneralThresholdChanged_UsesIt` | general threshold changed | `Store.GetAsync`, Settings changed | uses it |
| `GetAsync_PolicyThresholdSet_OverridesGeneralThreshold` | policy threshold set | `Store.GetAsync`, Settings changed | overrides general threshold |
| `GetAsync_KeyHasQueuedWrite_SkipsRefresh` | key has queued write | `Store.GetAsync` | skips refresh |
| `GetAsync_RefreshLoaderThrows_KeepsCurrentValueAndLogsWarning` | refresh loader throws | `Store.GetAsync`, `Loader.LoadAsync`, `IBackgroundWork.Run` captures the work | keeps current value and logs warning |
| `GetAsync_ReadAfterFailedRefresh_TriesAgain` | read after failed refresh | `Store.GetAsync`, `Loader.LoadAsync`, `IBackgroundWork.Run` captures the work | tries again |
| `GetAsync_ReadsWhileRefreshRunning_StartOneRefresh` | reads while refresh running | `Store.GetAsync` | start one refresh |
| `GetAsync_Hit_RecordsHit` | hit | `Store.GetAsync` | records hit |
| `GetAsync_Miss_RecordsMiss` | miss | `Store.GetAsync` | records miss |
| `GetAsync_StoreFails_RecordsError` | store fails | `Store.GetAsync` throws | records error |

### `Cache<T>.StoreInCacheAsync`
| Test name | Scenario | Mocks set up | Expected result |
|-----------|----------|--------------|-----------------|
| `StoreInCacheAsync_NullKey_ThrowsArgumentNullException` | null key | Harness defaults | throws argument null exception |
| `StoreInCacheAsync_NullValue_ThrowsArgumentNullException` | null value | Harness defaults | throws argument null exception |
| `StoreInCacheAsync_EmptyKey_ThrowsArgumentException` | empty key | Harness defaults | throws argument exception |
| `StoreInCacheAsync_KeyOver1024Characters_ThrowsArgumentException` | key over1024 characters | Harness defaults | throws argument exception |
| `StoreInCacheAsync_NonPositiveTtl_ThrowsArgumentOutOfRangeException` (2 cases) | non positive ttl | Harness defaults | throws argument out of range exception |
| `StoreInCacheAsync_TtlOverMaxTtl_ThrowsArgumentOutOfRangeException` | ttl over max ttl | Harness defaults | throws argument out of range exception |
| `StoreInCacheAsync_ReadStrategyIsNotCacheAside_ThrowsInvalidOperationException` | read strategy is not cache aside | Harness defaults | throws invalid operation exception |
| `StoreInCacheAsync_CacheAside_StoresValueWithPrefixedKey` | cache aside | Harness defaults | stores value with prefixed key |
| `StoreInCacheAsync_PolicyAlsoHasWriteThrough_DoesNotCallWriter` | policy also has write through | Harness defaults | does not call writer |
| `StoreInCacheAsync_TtlGiven_UsesIt` | ttl given | Harness defaults | uses it |
| `StoreInCacheAsync_TtlEqualsMaxTtl_IsAccepted` | ttl equals max ttl | Harness defaults | is accepted |
| `StoreInCacheAsync_PolicyRaisesMaxTtl_AcceptsLongerTtl` | policy raises max ttl | Settings changed | accepts longer ttl |
| `StoreInCacheAsync_ValueOverMaxValueBytes_ReturnsValueTooLarge` | value over max value bytes | Settings changed | returns value too large |
| `StoreInCacheAsync_ValueExactlyAtMaxValueBytes_IsStored` | value exactly at max value bytes | Settings changed | is stored |
| `StoreInCacheAsync_SerializerThrows_ReturnsSerializationFailed` | serializer throws | `Serializer` | returns serialization failed |
| `StoreInCacheAsync_StoreFails_ReturnsStoreUnavailable` | store fails | `Store.SetAsync` | returns store unavailable |
| `StoreInCacheAsync_CancelledToken_ThrowsOperationCanceledException` | cancelled token | Harness defaults | throws operation canceled exception |
| `StoreInCacheAsync_AnyValue_PassesCurrentTimeAsCreatedAt` | any value | Harness defaults | passes current time as created at |

### `Cache<T>.WriteAsync`
| Test name | Scenario | Mocks set up | Expected result |
|-----------|----------|--------------|-----------------|
| `WriteAsync_NullKey_ThrowsArgumentNullException` | null key | Harness defaults | throws argument null exception |
| `WriteAsync_NullValue_ThrowsArgumentNullException` | null value | Harness defaults | throws argument null exception |
| `WriteAsync_EmptyKey_ThrowsArgumentException` | empty key | Harness defaults | throws argument exception |
| `WriteAsync_KeyOver1024Characters_ThrowsArgumentException` | key over1024 characters | Harness defaults | throws argument exception |
| `WriteAsync_NonPositiveTtl_ThrowsArgumentOutOfRangeException` (2 cases) | non positive ttl | Harness defaults | throws argument out of range exception |
| `WriteAsync_TtlOverMaxTtl_ThrowsArgumentOutOfRangeException` | ttl over max ttl | Harness defaults | throws argument out of range exception |
| `WriteAsync_PolicyHasNoWriteStrategy_ThrowsInvalidOperationException` | policy has no write strategy | Harness defaults | throws invalid operation exception |
| `WriteAsync_CancelledToken_ThrowsOperationCanceledException` | cancelled token | Harness defaults | throws operation canceled exception |
| `WriteAsync_WriteThrough_WritesDatabaseBeforeCache` | write through | `Writer.WriteAsync`, `Store.SetAsync` | writes database before cache |
| `WriteAsync_WriteThroughTtlGiven_StoresWithIt` | write through ttl given | Harness defaults | stores with it |
| `WriteAsync_WriteThroughWriterThrows_ReturnsWriterFailedAndLeavesCache` | write through writer throws | `Writer.WriteAsync` throws | returns writer failed and leaves cache |
| `WriteAsync_WriteThroughCacheFails_ReportsSuccessLogsWarningAndRemovesOldEntry` | write through cache fails | `Store.SetAsync` throws | reports success logs warning and removes old entry |
| `WriteAsync_WriteThroughCacheAndRemoveFail_StillReportsSuccess` | write through cache and remove fail | `Store.SetAsync` throws, `Store.RemoveAsync` | still reports success |
| `WriteAsync_WriteThroughValueTooLarge_ReturnsErrorBeforeWritingDatabase` | write through value too large | Settings changed | returns error before writing database |
| `WriteAsync_WriteThroughSerializerThrows_ReturnsErrorBeforeWritingDatabase` | write through serializer throws | `Serializer` | returns error before writing database |
| `WriteAsync_WriteBehind_QueuesWriteAndReportsSuccess` | write behind | Settings changed | queues write and reports success |
| `WriteAsync_WriteBehind_DoesNotCallWriter` | write behind | Harness defaults | does not call writer |
| `WriteAsync_WriteBehindServiceNotRunning_ThrowsInvalidOperationException` | write behind service not running | `IWriteBehindStatus.IsRunning` | throws invalid operation exception |
| `WriteAsync_WriteBehindStoreFails_ReturnsStoreUnavailableAndDoesNotCallWriter` | write behind store fails | `Store.TryEnqueueAsync` | returns store unavailable and does not call writer |
| `WriteAsync_WriteBehindQueueFull_ReturnsQueueFull` | write behind queue full | `Store.TryEnqueueAsync` | returns queue full |
| `WriteAsync_WriteBehindValueTooLarge_ReturnsErrorAndQueuesNothing` | write behind value too large | Settings changed | returns error and queues nothing |
| `WriteAsync_WriteAround_WritesDatabaseThenRemovesEntry` | write around | `Writer.WriteAsync`, `Store.RemoveAsync` | writes database then removes entry |
| `WriteAsync_WriteAround_StoresNothingInCache` | write around | Harness defaults | stores nothing in cache |
| `WriteAsync_WriteAroundWriterThrows_ReturnsWriterFailedAndKeepsEntry` | write around writer throws | `Writer.WriteAsync` throws | returns writer failed and keeps entry |
| `WriteAsync_WriteAroundRemoveFails_ReportsSuccessAndLogsWarning` | write around remove fails | `Store.RemoveAsync` | reports success and logs warning |
| `WriteAsync_WriteAroundValueOverMaxValueBytes_IsStillWritten` | write around value over max value bytes | Settings changed | is still written |
| `WriteAsync_CacheAsideWithWriteThrough_FollowsWriteStrategy` | cache aside with write through | Harness defaults | follows write strategy |
| `WriteAsync_WriterThrows_RecordsError` | writer throws | `Writer.WriteAsync` throws | records error |

### `Cache<T>.RemoveAsync`
| Test name | Scenario | Mocks set up | Expected result |
|-----------|----------|--------------|-----------------|
| `RemoveAsync_NullKey_ThrowsArgumentNullException` | null key | Harness defaults | throws argument null exception |
| `RemoveAsync_EmptyKey_ThrowsArgumentException` | empty key | Harness defaults | throws argument exception |
| `RemoveAsync_KeyOver1024Characters_ThrowsArgumentException` | key over1024 characters | Harness defaults | throws argument exception |
| `RemoveAsync_CancelledToken_ThrowsOperationCanceledException` | cancelled token | Harness defaults | throws operation canceled exception |
| `RemoveAsync_EntryExists_RemovesWithPrefixedKey` | entry exists | `Store.RemoveAsync` | removes with prefixed key |
| `RemoveAsync_NoEntry_StillSucceeds` | no entry | `Store.RemoveAsync` | still succeeds |
| `RemoveAsync_StoreSupportsWriteBehind_CancelsQueuedWrite` | store supports write behind | Harness defaults | cancels queued write |
| `RemoveAsync_AnyPolicy_DoesNotTouchDatabase` | any policy | Harness defaults | does not touch database |
| `RemoveAsync_StoreFails_ReturnsStoreUnavailable` | store fails | `Store.RemoveAsync` | returns store unavailable |

### `WriteBehindProcessor`
| Test name | Scenario | Mocks set up | Expected result |
|-----------|----------|--------------|-----------------|
| `FlushOnceAsync_NothingDue_ReturnsZero` | nothing due | `Store.ClaimAsync` | returns zero |
| `FlushOnceAsync_FewerThanBatchWaiting_ClaimsOnlyWritesPastMaxDelay` | fewer than batch waiting | `Store.ClaimAsync`, `Store.GetQueueLengthAsync`, Settings changed | claims only writes past max delay |
| `FlushOnceAsync_FullBatchWaiting_ClaimsWithoutWaitingForMaxDelay` | full batch waiting | `Store.ClaimAsync`, `Store.GetQueueLengthAsync`, Settings changed | claims without waiting for max delay |
| `FlushOnceAsync_ClaimedWrite_CallsWriterWithKeyWithoutPrefix` | claimed write | `Store.ClaimAsync` | calls writer with key without prefix |
| `FlushOnceAsync_WriterSucceeds_CompletesWrite` | writer succeeds | `Store.ClaimAsync` | completes write |
| `FlushOnceAsync_SeveralWrites_CallsWriterOncePerWrite` | several writes | `Store.ClaimAsync` | calls writer once per write |
| `FlushOnceAsync_OneWriteFails_StillProcessesOthers` | one write fails | `Writer.WriteAsync`, `Store.ClaimAsync` | still processes others |
| `FlushOnceAsync_FirstTryFails_RetriesAfterInitialDelay` | first try fails | `Writer.WriteAsync` throws, `Store.ClaimAsync` | retries after initial delay |
| `FlushOnceAsync_ThirdTryFails_DoublesDelayTwice` | third try fails | `Writer.WriteAsync` throws, `Store.ClaimAsync` | doubles delay twice |
| `FlushOnceAsync_DelayWouldPassMaxDelay_UsesMaxDelay` | delay would pass max delay | `Writer.WriteAsync` throws, `Store.ClaimAsync`, Settings changed | uses max delay |
| `FlushOnceAsync_FinalTryFails_MovesToDeadLetterWithRetention` | final try fails | `Writer.WriteAsync` throws, `Store.ClaimAsync` | moves to dead letter with retention |
| `FlushOnceAsync_FinalTryFails_LogsError` | final try fails | `Writer.WriteAsync` throws, `Store.ClaimAsync` | logs error |
| `FlushOnceAsync_FinalTryFails_NotifiesHandler` | final try fails | `Writer.WriteAsync` throws, `Store.ClaimAsync` | notifies handler |
| `FlushOnceAsync_HandlerThrows_LogsAndKeepsDeadLetterWrite` | handler throws | `Writer.WriteAsync` throws, `Store.ClaimAsync`, `DeadLetterHandler.HandleAsync` | logs and keeps dead letter write |
| `FlushOnceAsync_QueuedValueCannotBeRead_MovesToDeadLetterWithoutRetrying` | queued value cannot be read | `Store.ClaimAsync`, `Serializer` | moves to dead letter without retrying |
| `FlushOnceAsync_QueuedValueTypeNotRegistered_MovesToDeadLetter` | queued value type not registered | `Store.ClaimAsync` | moves to dead letter |
| `FlushOnceAsync_WriterThrows_RecordsError` | writer throws | `Writer.WriteAsync` throws, `Store.ClaimAsync` | records error |
| `FlushAllAsync_WritesQueued_FlushesUntilEmpty` | writes queued | `Store.ClaimAsync` | flushes until empty |
| `FlushAllAsync_AnyWrite_DoesNotWaitForMaxFlushDelay` | any write | `Store.ClaimAsync` | does not wait for max flush delay |
| `FlushAllAsync_ShutdownTimeoutPasses_Stops` | shutdown timeout passes | `Store.ClaimAsync`, Settings changed, Clock moved | stops |

### `AddCacheMe, CacheMeBuilder, CachePolicyBuilder<T> and options validation`
| Test name | Scenario | Mocks set up | Expected result |
|-----------|----------|--------------|-----------------|
| `AddCacheMe_TypeRegistered_ResolvesCache` | type registered | Real DI container; in-memory or JSON configuration | resolves cache |
| `AddCacheMe_TypeRegistered_RegistersCacheAsSingleton` | type registered | Real DI container; in-memory or JSON configuration | registers cache as singleton |
| `AddCacheMe_TypeNotRegistered_FailsWhenResolved` | type not registered | Real DI container; in-memory or JSON configuration | fails when resolved |
| `ForType_LoaderNotRegistered_RegistersItAsScoped` | loader not registered | Real DI container; in-memory or JSON configuration | registers it as scoped |
| `ForType_LoaderAlreadyRegistered_KeepsRegistration` | loader already registered | Real DI container; in-memory or JSON configuration | keeps registration |
| `AddCacheMe_JsonConfiguration_BindsAllSettings` | json configuration | Real DI container; in-memory or JSON configuration | binds all settings |
| `Configure_SettingAlsoInJson_CodeWins` | setting also in json | Real DI container; in-memory or JSON configuration | code wins |
| `AddCacheMe_CodeOnly_UsesConfigureAndConnectionString` | code only | Real DI container; in-memory or JSON configuration | uses configure and connection string |
| `Options_KeyPrefixInvalid_FailsValidation` (2 cases) | key prefix invalid | Real DI container; in-memory or JSON configuration | fails validation |
| `Options_KeyPrefixOver128Characters_FailsValidation` | key prefix over128 characters | Real DI container; in-memory or JSON configuration | fails validation |
| `Options_DefaultTtlMissing_FailsValidation` | default ttl missing | Real DI container; in-memory or JSON configuration | fails validation |
| `Options_DefaultTtlOverMaxTtl_FailsValidation` | default ttl over max ttl | Real DI container; in-memory or JSON configuration | fails validation |
| `Options_PolicyDefaultTtlOverPolicyMaxTtl_NamesPolicyInFailure` | policy default ttl over policy max ttl | Real DI container; in-memory or JSON configuration | names policy in failure |
| `Options_PolicyRaisesMaxTtl_PassesValidation` | policy raises max ttl | Real DI container; in-memory or JSON configuration | passes validation |
| `Options_SettingOutOfRange_FailsValidation` (15 cases) | setting out of range | Real DI container; in-memory or JSON configuration | fails validation |
| `Options_SettingAtBoundary_PassesValidation` (3 cases) | setting at boundary | Real DI container; in-memory or JSON configuration | passes validation |
| `Options_SeveralInvalidSettings_ReportsAllTogether` | several invalid settings | Real DI container; in-memory or JSON configuration | reports all together |
| `Options_UnknownKey_ThrowsInvalidOperationExceptionNamingIt` | unknown key | Real DI container; in-memory or JSON configuration | throws invalid operation exception naming it |
| `Options_PolicyKeyHasNoRegisteredType_FailsValidation` | policy key has no registered type | Real DI container; in-memory or JSON configuration | fails validation |
| `Options_ThresholdSetForTypeWithoutRefreshAhead_FailsValidation` | threshold set for type without refresh ahead | Real DI container; in-memory or JSON configuration | fails validation |
| `Options_UseRedisWithoutConnectionString_FailsValidation` | use redis without connection string | Real DI container; in-memory or JSON configuration | fails validation |
| `Options_ConnectionStringListsTwoEndpoints_FailsValidation` | connection string lists two endpoints | Real DI container; in-memory or JSON configuration | fails validation |
| `Options_ConnectionStringInvalid_DoesNotPutItInMessage` | connection string invalid | Real DI container; in-memory or JSON configuration | does not put it in message |
| `UseRedis_ExistingConnection_DoesNotNeedConnectionString` | existing connection | Real DI container; `Mock<IConnectionMultiplexer>` | does not need connection string |
| `UseStore_CustomStore_DoesNotNeedRedisSettings` | custom store | Real DI container; in-memory or JSON configuration | does not need redis settings |
| `AddCacheMe_TwoStores_ThrowsInvalidOperationException` | two stores | Real DI container; in-memory or JSON configuration | throws invalid operation exception |
| `AddCacheMe_NoStore_ThrowsInvalidOperationException` | no store | Real DI container; in-memory or JSON configuration | throws invalid operation exception |
| `AddCacheMe_WriteBehindWithPlainStore_ThrowsInvalidOperationException` | write behind with plain store | Real DI container; in-memory or JSON configuration | throws invalid operation exception |
| `ForType_SameTypeTwice_ThrowsInvalidOperationException` | same type twice | Real DI container; in-memory or JSON configuration | throws invalid operation exception |
| `ForType_SameSettingsKeyTwice_ThrowsInvalidOperationException` | same settings key twice | Real DI container; in-memory or JSON configuration | throws invalid operation exception |
| `ForType_NoStrategy_ThrowsInvalidOperationException` | no strategy | Real DI container; in-memory or JSON configuration | throws invalid operation exception |
| `CacheAside_ReadStrategyAlreadySet_ThrowsInvalidOperationException` | read strategy already set | Real DI container; in-memory or JSON configuration | throws invalid operation exception |
| `ReadThrough_ReadStrategyAlreadySet_ThrowsInvalidOperationException` | read strategy already set | Real DI container; in-memory or JSON configuration | throws invalid operation exception |
| `WriteBehind_WriteStrategyAlreadySet_ThrowsInvalidOperationException` | write strategy already set | Real DI container; in-memory or JSON configuration | throws invalid operation exception |
| `WriteAround_WriteStrategyAlreadySet_ThrowsInvalidOperationException` | write strategy already set | Real DI container; in-memory or JSON configuration | throws invalid operation exception |
| `WriteThrough_WriteStrategyAlreadySet_ThrowsInvalidOperationException` | write strategy already set | Real DI container; in-memory or JSON configuration | throws invalid operation exception |
| `RefreshAhead_NoReadStrategy_ThrowsInvalidOperationException` | no read strategy | Real DI container; in-memory or JSON configuration | throws invalid operation exception |
| `RefreshAhead_WithCacheAside_ThrowsInvalidOperationException` | with cache aside | Real DI container; in-memory or JSON configuration | throws invalid operation exception |
| `ForType_ReadAndWriteStrategy_Registers` | read and write strategy | Real DI container; in-memory or JSON configuration | registers |
| `UseSerializer_CustomSerializer_ReplacesDefault` | custom serializer | Real DI container; in-memory or JSON configuration | replaces default |
| `UseDeadLetterHandler_AnyHandler_RegistersItAsScoped` | any handler | Real DI container; in-memory or JSON configuration | registers it as scoped |
| `AddCacheMe_RedisStore_RegistersDeadLetterStore` | redis store | Real DI container; in-memory or JSON configuration | registers dead letter store |
| `AddCacheMe_PlainStore_DoesNotRegisterDeadLetterStore` | plain store | Real DI container; in-memory or JSON configuration | does not register dead letter store |
| `AddCacheMe_NoClockRegistered_UsesSystemClock` | no clock registered | Real DI container; in-memory or JSON configuration | uses system clock |
| `AddCacheMe_ClockAlreadyRegistered_KeepsIt` | clock already registered | Real DI container; in-memory or JSON configuration | keeps it |
| `AddCacheMe_ValidJson_PassesValidation` | valid json | Real DI container; in-memory or JSON configuration | passes validation |

### `Models, options defaults and metric names`
| Test name | Scenario | Mocks set up | Expected result |
|-----------|----------|--------------|-----------------|
| `Found_AnyValue_SetsStatusAndValue` | any value | None | sets status and value |
| `NotFound_NoArguments_SetsStatusOnly` | no arguments | None | sets status only |
| `ReadFailed_AnyError_SetsStatusAndError` | any error | None | sets status and error |
| `Success_NoArguments_SetsSucceeded` | no arguments | None | sets succeeded |
| `WriteFailed_AnyError_SetsError` | any error | None | sets error |
| `CacheErrorCtor_AllArguments_StoresThem` | all arguments | None | stores them |
| `CacheMeOptions_NoSettings_HasApprovedDefaults` | no settings | None | has approved defaults |
| `CacheMeOptions_PolicyKeys_AreCaseInsensitive` | policy keys | None | are case insensitive |
| `CacheMeMetrics_Names_MatchApprovedContract` | names | None | match approved contract |

### `Redis container tests`
| Test name | Scenario | Mocks set up | Expected result |
|-----------|----------|--------------|-----------------|
| `GetAsync_ReadThroughMiss_StoresEntryUnderPrefixedKeyWithTtl` | read through miss | Real Redis container; recording loader, writer and handler | stores entry under prefixed key with ttl |
| `GetAsync_ReadThroughSecondRead_DoesNotCallLoaderAgain` | read through second read | Real Redis container; recording loader, writer and handler | does not call loader again |
| `StoreInCacheAsync_CacheAside_ValueRoundTrips` | cache aside | Real Redis container; recording loader, writer and handler | value round trips |
| `GetAsync_EntryCannotBeRead_ReturnsNotFound` | entry cannot be read | Real Redis container; recording loader, writer and handler | returns not found |
| `GetAsync_EntryPastThreshold_ReloadsInBackground` | entry past threshold | Real Redis container; recording loader, writer and handler | reloads in background |
| `WriteAsync_WriteThrough_WritesDatabaseAndCache` | write through | Real Redis container; recording loader, writer and handler | writes database and cache |
| `WriteAsync_WriteAround_RemovesExistingEntry` | write around | Real Redis container; recording loader, writer and handler | removes existing entry |
| `RemoveAsync_EntryExists_RemovesIt` | entry exists | Real Redis container; recording loader, writer and handler | removes it |
| `WriteAsync_WriteBehind_WritesDatabaseInBackground` | write behind | Real Redis container; recording loader, writer and handler | writes database in background |
| `WriteAsync_FullBatchWaiting_FlushesBeforeMaxDelay` | full batch waiting | Real Redis container; recording loader, writer and handler | flushes before max delay |
| `WriteAsync_TwoWritesToOneKeyBeforeFlush_WritesLatestOnce` | two writes to one key before flush | Real Redis container; recording loader, writer and handler | writes latest once |
| `WriteAsync_WriteArrivesWhileFlushing_NewestValueEndsUpLast` | write arrives while flushing | Real Redis container; recording loader, writer and handler | newest value ends up last |
| `WriteAsync_WriteQueued_EntryHasNoExpiryUntilCompleted` | write queued | Real Redis container; recording loader, writer and handler | entry has no expiry until completed |
| `StopAsync_WritesQueued_FlushesThem` | writes queued | Real Redis container; recording loader, writer and handler | flushes them |
| `WriteAsync_QueueFull_ReturnsQueueFull` | queue full | Real Redis container; recording loader, writer and handler | returns queue full |
| `WriteAsync_QueueFullAndKeyAlreadyQueued_ReplacesValue` | queue full and key already queued | Real Redis container; recording loader, writer and handler | replaces value |
| `RemoveAsync_WriteQueued_CancelsIt` | write queued | Real Redis container; recording loader, writer and handler | cancels it |
| `WriteAsync_WriterKeepsFailing_MovesToDeadLetterStoreAndNotifiesHandler` | writer keeps failing | Real Redis container; recording loader, writer and handler | moves to dead letter store and notifies handler |
| `DeleteAsync_StoredFailedWrite_RemovesIt` | stored failed write | Real Redis container; recording loader, writer and handler | removes it |
| `WriteAsync_TwoInstancesShareQueue_EachWriteHappensOnce` | two instances share queue | Real Redis container; recording loader, writer and handler | each write happens once |
| `WriteAsync_InstanceCrashes_AnotherInstanceFinishesWrite` | instance crashes | Real Redis container; recording loader, writer and handler | another instance finishes write |
| `StartAsync_EvictionPolicyCanRemoveQueue_LogsWarning` | eviction policy can remove queue | Real Redis container; recording loader, writer and handler | logs warning |
| `UseRedis_ExistingConnection_Works` | existing connection | Real Redis container; recording loader, writer and handler | works |
| `WriteAsync_NoHostStarted_ThrowsInvalidOperationException` | no host started | Real Redis container; recording loader, writer and handler | throws invalid operation exception |
| `GetAsync_RedisUnreachableReadThrough_FallsBackAndLogsWarning` | redis unreachable read through | Real Redis container; recording loader, writer and handler | falls back and logs warning |
| `GetAsync_RedisUnreachableCacheAside_ReturnsStoreUnavailable` | redis unreachable cache aside | Real Redis container; recording loader, writer and handler | returns store unavailable |
| `GetAsync_RedisUnreachable_FailsWithinOperationTimeout` | redis unreachable | Real Redis container; recording loader, writer and handler | fails within operation timeout |
| `StartAsync_RedisUnreachable_DoesNotStopApplicationAndLogsWarning` | redis unreachable | Real Redis container; recording loader, writer and handler | does not stop application and logs warning |
| `GetAsync_ManyConcurrentReaders_AllGetValue` | many concurrent readers | Real Redis container; recording loader, writer and handler | all get value |

## Baseline run result
| Result | Count |
|--------|-------|
| Total | 217 |
| Failing as expected: `NotImplementedException` | 213 |
| Failing as expected: stub constructor has no logic | 0 |
| Passing (each explained below) | 4 |
| Unexpected failures (must be 0) | 0 |

**Stub constructor tests:** none. No test asserts constructor validation, because the API document specifies none.

**Passing tests and why:**
- `CacheMeMetrics_Names_MatchApprovedContract`: checks constants, which Stage 2 approved as part of the type shape.
- `CacheMeOptions_NoSettings_HasApprovedDefaults`: checks default property values, which Stage 2 approved as part of the type shape.
- `CacheErrorCtor_AllArguments_StoresThem`: checks a constructor that only stores its arguments, which the stub already does.
- `CacheMeOptions_PolicyKeys_AreCaseInsensitive`: checks the dictionary default, which Stage 2 approved as part of the type shape.

**What this baseline proves:** everything compiles, every test is discovered, and the Redis container starts. It does not prove the tests are right. Many tests stop at their first stub call, so their later arrangements and assertions have not run yet, and Stage 5 may still expose a defect in a test.

## Gaps
| Requirement | Why it could not be tested here |
|-------------|---------------------------------|
| NFR-001 | A performance target needs a benchmark against a finished library. A pass or fail in a test run would be unreliable. |
| NFR-005 | Tests cover concurrent reads and misses, but cannot prove thread-safety for all interleavings. |
| NFR-007 | Tests cover restarts and warnings. They do not cover Redis itself losing data, which the brief accepts as a risk. |
| TD-001, TD-002, TD-003, TD-012, TD-014 | These are project settings and documents, not behaviour. The build and Stage 5 cover them. |

## Assumptions

These are design choices for you to correct if they are wrong.

**Behaviour these tests fix, which earlier stages left open**

- **Oversized and unserializable values fail before the database is touched** (write-through). Otherwise, a write could be saved and still report an error.
- **Read-through still returns the value if storing it in the cache fails,** and logs a warning. This extends FR-012.
- **One refresh runs at a time per key, per process.** Reads during a refresh do not start another (FR-015).
- **A queued write that cannot be read, or whose value type is no longer registered, goes straight to the dead-letter store** without retries, because retrying cannot help.
- **The retry delay is `InitialDelay` × 2^(tries failed before this one), capped at `MaxDelay`,** so the default waits are 1, 2, 4 and 8 seconds. A dead-letter write's `Attempts` includes the final try.
- **A shutdown flush ignores `MaxFlushDelay`,** and stops once `ShutdownTimeout` passes.
- **A flush ignores `MaxFlushDelay` when a full batch is waiting,** as FR-022 says "or sooner".

**Changes outside the test project**

- **`global.json` is new at the repository root.** The .NET 10 SDK refuses to run xUnit v3 4.x through the old test runner without it. It sets only the test runner and does not pin an SDK version.
- **`CachePolicy<T>` and `CachePolicyRegistry` are no longer empty.** Tests need to build policies, so the first now has the properties the API document describes, with two internal enums, and the second has a stub `Add<T>` method. There is no logic and no change to the public API.

**Test tooling**

- `Testcontainers.Redis` and `Microsoft.Extensions.Hosting` are added for the Redis container tests that Stage 0 chose (TD-013).
- A small `TestClock` replaces `FakeTimeProvider`, which avoids another package.
- `xunit.runner.visualstudio` and `Microsoft.NET.Test.Sdk` stay in the project for Visual Studio Test Explorer support, as the workflow names them.

## Open Questions

None.

## Decisions

None. This stage raised no questions.
