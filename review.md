# PR #580 Review: Upgrade to Linq2Db 6 with AsQueryable Tag Insert Enhancement

**PR:** akkadotnet/Akka.Persistence.Sql#580
**Author:** Drew (@to11mtm)
**Branch:** `l2db-v6-insert-example-nonparameterized` -> `dev`
**Status:** Open (Changes Requested by @Arkatufus)
**Stats:** +1603 / -78 across 23 files

---

## New Features

### Feature 1: Linq2Db 6 Upgrade

**Files:** `Directory.Packages.props`, `AkkaPersistenceDataConnectionFactory.cs`, `Options.cs`, `MySqlContainer.cs`, various files adding `using LinqToDB.Async`

Upgrades the linq2db dependency from **5.4.1.9 to 6.2.1**. This is a major version bump that requires several compatibility adjustments:

- **RetryPolicy API change:** `record with { }` syntax replaced with `.WithRetryPolicy()` method calls in `AkkaPersistenceDataConnectionFactory.cs`
- **MySQL provider name changes:** `ProviderName.MySqlOfficial` -> `ProviderName.MySql80MySqlData`, `ProviderName.MySqlConnector` -> `ProviderName.MySql80MySqlConnector`, `ProviderName.MySqlOfficial` -> `ProviderName.MySql80` in test container
- **Namespace reorganization:** Several `using LinqToDB.Async` additions needed where extension methods moved (snapshot DAO, read journal DAO, tag table migrator, etc.)
- **Clone connection broken:** `AkkaDataConnection.Clone()` now throws `NotImplementedException` with a `// TODO FIX` comment -- this is commented out along with the `use-clone-connection` code path in `GetConnection()`
- Adds `Microsoft.Bcl.AsyncInterfaces 8.0.0` package dependency

### Feature 2: AsQueryable Literal Insert for Tag Table Mode

**Files:** `BaseByteArrayJournalDao.cs`, `AkkaDataConnection.cs`, `Linq2DbHacks.cs`, `BaseByteArrayJournalDaoConfig.cs`, `persistence.conf`, `SqlJournalOptions.cs`

A new opt-in optimization for tag table writes. When `use-tagtable-asqueryable-literal-insert = true`, the journal DAO uses `AsQueryable()` + `InsertWithOutputAsync()` to batch event inserts with their tags in fewer database round-trips.

**How it works:**
1. Events are accumulated into a buffer (tracked by byte size and row count)
2. When a threshold is reached (`tagtable-asqueryable-insert-sql-length-limit`, default 5MB), the batch is flushed
3. Events are inserted via `AsQueryable()` which embeds values as SQL literals (non-parameterized) into an `INSERT ... OUTPUT` statement
4. The `OUTPUT` clause returns the generated `Ordering` column, which is then used to build the corresponding tag rows
5. Tag rows are bulk-inserted via `BulkCopyAsync` with `MultipleRows` mode

**Key implementation details:**
- Introduces `JournalRowIns` -- a projection DTO that excludes the `Ordering` column (which is DB-generated) and the `Tags` field (which goes to a separate table)
- Uses `ArrayPool<JournalRow>.Shared` for buffer management to avoid allocations
- Supports provider-specific detection: only enabled for SqlServer, PostgreSQL, and SQLite providers (not MySQL)
- Custom `InsertWithOutputListAsync` extension method in `Linq2DbHacks.cs` that returns `List<T>` instead of `IAsyncEnumerable<T>` (works around sync-ish behavior in stock linq2db)
- Handles `WriterUuid` column conditionally based on `UseWriterUuidColumn` config

**New HOCON config:**
```hocon
use-tagtable-asqueryable-literal-insert = false  # opt-in
tagtable-asqueryable-insert-sql-length-limit = 5000000  # 5MB default
```

**New Hosting options:**
- `SqlJournalOptions.UseTagTableAsQueryableLiteralInsert`
- `SqlJournalOptions.AsQueryableInsertSqlLengthLimit`

### Feature 3: ChannelBatchQueue -- Replacement for Source.Queue + BatchWeighted

**Files:** `ChannelQueueWithBatch.cs`, `ChannelQueueWithBatch.md`, `BaseByteArrayJournalDao.cs`

Replaces the Akka Streams-based write pipeline input (`Source.Queue<WriteQueueEntry>` + `BatchWeighted`) with a `System.Threading.Channels`-based implementation.

**Previous architecture:**
```
Source.Queue(BufferSize, OverflowStrategy.DropNew)
  .BatchWeighted(BatchSize, costFunc, seed, aggregate)
  .SelectAsync(Parallelism, handler)
```

**New architecture:**
```
Channel.CreateBounded<WriteQueueEntry>(BufferSize, FullMode=Wait)
  -> ChannelQueueWithBatch(reader, BatchSize, ...)  // IS a ChannelReader<TBatch>
  -> Source.ChannelReader(batcher)
     .Buffer(Parallelism, Backpressure)
     .SelectAsync(Parallelism, handler)
```

**Key improvements:**
- **Backpressure instead of drop:** `BoundedChannelFullMode.Wait` replaces `OverflowStrategy.DropNew` -- writes block instead of being silently dropped
- **Better batching:** `ChannelQueueWithBatch` eagerly drains all available items up to the weight budget on each `TryRead()`, avoiding the single-element batch problem where `SelectAsync` pulling fast would defeat batching
- **Simpler error handling:** `TryWrite` returning false replaces the 4-case `QueueOfferResult` switch
- **Overflow item parking:** Mirrors `EagerBatchStage._pending` semantics -- an item that exceeds the batch budget becomes the seed of the next batch

The `ChannelQueueWithBatch<TInput, TBatch>` class extends `ChannelReader<TBatch>` directly, requiring no background tasks or output channels. Thread safety is via a simple `lock` over synchronous critical sections.

### Feature 4: Expanded Benchmark Test Suite

**Files:** `CmdEventTagger.cs`, `PostgreSqlSqlJournalPerfSpec.cs`, `SqlServerLinq2DbJournalPerfSpec.cs`, `SqlJournalPerfSpec.cs`, `TestConstants.cs`

New benchmark test variants to measure the performance impact of the changes:

- **CmdEventTagger:** `IWriteEventAdapter` that tags every `Cmd` with 2 tags (`perf-tag-1`, `perf-tag-2`)
- **Payload support:** `Cmd` now accepts an optional `byte[]` data payload; `SqlJournalPerfSpec` supports a `payloadSizeBytes` parameter (default: 0, large payload tests use 1KB)
- **New PostgreSQL test classes:**
  - `PostgreSqlSqlCsvTaggedJournalPerfSpec` -- CSV mode with 2 tags/event
  - `PostgreSqlSqlTagTableAsQueryableJournalPerfSpec` -- TagTable + AsQueryable, no tags
  - `PostgreSqlSqlTagTableTaggedJournalPerfSpec` -- TagTable with 2 tags/event (no AsQueryable)
  - `PostgreSqlSqlTagTableAsQueryableTaggedJournalPerfSpec` -- TagTable + AsQueryable + 2 tags/event
  - `PostgreSqlSqlCsvLargePayloadJournalPerfSpec` -- CSV with 1KB payload
  - `PostgreSqlSqlTagTableLargePayloadJournalPerfSpec` -- TagTable with 1KB payload
  - `PostgreSqlSqlTagTableLargePayloadTaggedJournalPerfSpec` -- TagTable + 1KB + 2 tags
  - `PostgreSqlSqlTagTableAsQueryableLargePayloadTaggedJournalPerfSpec` -- TagTable + AsQueryable + 1KB + 2 tags
- **Equivalent SQL Server test classes** (same matrix)

**Bug fix found:** `SqlServerLinq2DbTagTableJournalPerfSpec` was previously using `TagMode.Csv` instead of `TagMode.TagTable` -- corrected in this PR.

---

## Open Discussion Points from PR Comments

The PR has extensive self-review comments from @to11mtm and review questions from @Arkatufus. These highlight several unresolved decisions:

### @Arkatufus Review (Changes Requested)

1. **Clone connection public API concern** (`AkkaDataConnection.cs`): @Arkatufus asks whether axing `Clone()` affects runtime behavior, and suggests using a `[Deprecated]` attribute + custom cloning implementation for at least a few versions rather than throwing `NotImplementedException`, since this is a public API.

2. **Older database compatibility** (`BaseByteArrayJournalDaoConfig.cs`): @Arkatufus asks whether the AsQueryable feature is backward compatible with older database versions, citing the earlier `STRING_AGG` incident with MSSQL 2016. This is a valid concern -- `INSERT ... OUTPUT` syntax should be fine on older MSSQL versions, but needs explicit verification.

3. **Channel lifetime** (`BaseByteArrayJournalDao.cs`): @Arkatufus asks whether the input channel should ever be closed, noting that the journal is supposed to be alive until actor system termination. @to11mtm had the same half-concern, noting that the old `Source.Queue` was also never explicitly closed.

### @to11mtm Self-Review Notes

4. **SQL length limit uncertainty** (`persistence.conf`): @to11mtm himself notes "I don't know if I like this number" about the 5MB default for `tagtable-asqueryable-insert-sql-length-limit`. He also notes the per-provider BulkCopy limits from linq2db could be cribbed, and suspects a smaller limit might be more beneficial but didn't get to test variations in depth.

5. **2048 padding magic number** (`BaseByteArrayJournalDao.cs`): @to11mtm explains the 2048 accounts for serializer manifest (can get big with heavy generics), persistence ID, sequence number, timestamp, WriterUUID, and deleted flag. Acknowledges 1024 is "probably more real" but the overpadding isn't hurting much.

6. **INSERT OUTPUT ordering safety** (`BaseByteArrayJournalDao.cs`): @to11mtm raises whether the inserted results should have `OrderBy(insRow => insRow.OrderingId)` before the tag-join for data sanity. Notes that RDBMSes (especially MSSQL) don't guarantee output ordering on `INSERT WITH OUTPUT/RETURNING`. The current approach uses `(PersistenceId, SequenceNumber)` as the join key (guaranteed unique by constraints), which is safe, but ordering might matter for Query API consumers.

7. **Batch size observation** (`BaseByteArrayJournalDao.cs`): @to11mtm notes from benchmarks that smaller batch sizes are sometimes _far_ faster with tag mode enabled, with a screenshot showing the difference. This is relevant to the `maxWeight` parameter choice.

8. **`Linq2DbHacks.InsertWithOutputListAsync` controversy** (`Linq2DbHacks.cs`): @to11mtm acknowledges the class is "possibly controversial" but says the wiring is "WAYYY more efficient" than stock `InsertWithOutputAsync`, likely because it does less async-wrapping-sync. Notes "we could ditch it if we really needed to."

9. **Buffer stage justification** (`BaseByteArrayJournalDao.cs`): The `.Buffer(Parallelism, Backpressure)` stage added between ChannelReader and SelectAsync is defended as providing the most consistent performance across different batch size configs and scenarios, though it might introduce more single writes.

10. **Design doc to be removed** (`ChannelQueueWithBatch.md`): @to11mtm explicitly says "I will of course remove this" and suggests it could be useful for AI-assisted review of the design.

11. **Expression caching considered** (`BaseByteArrayJournalDao.cs`): @to11mtm considered making the `InsertWithOutput` expression trees static with switching logic to simplify IL, but held back thinking it might be "too much."

12. **AI copilot artifacts** (`BaseByteArrayJournalDao.cs`): @to11mtm acknowledges "My custom copilot setup is _so excited_ about this that my rules for how to write xmldocs were ignored" -- the "uwu" and emoji in xmldocs are copilot artifacts, not intentional.

---

## Review Concerns

### Critical Issues

1. **Clone connection disabled with TODO:** `AkkaDataConnection.Clone()` throws `NotImplementedException` and the clone path in `GetConnection()` is commented out. The `use-clone-connection` HOCON config still exists and users may have it set. **@Arkatufus has requested** this be handled with a deprecation attribute + interim implementation rather than a hard break.

2. **Internal API usage:** `Linq2DbHacks.cs` imports from `LinqToDB.Internal.*` namespaces (`LinqToDB.Internal.Async`, `LinqToDB.Internal.Linq`, `LinqToDB.Internal.DataProvider.*`). These are internal APIs that may break without notice in future linq2db releases. Similarly, `LinqExtensions.ProcessSourceQueryable` is accessed as a static field. @to11mtm acknowledges this is controversial but defends the performance benefit.

3. **No unit tests for `ChannelQueueWithBatch`:** The design doc (`ChannelQueueWithBatch.md`) includes a test plan with 11 test cases, all marked as unchecked. The implementation checklist also shows unit tests are incomplete.

4. **Older database version compatibility unverified:** @Arkatufus raised this concern and it hasn't been addressed yet. The `INSERT...OUTPUT` syntax used in the AsQueryable path needs to be verified against older MSSQL/PostgreSQL versions that this project supports.

### Moderate Issues

5. **`TryWrite` vs backpressure semantics:** The code uses `_inputChannel.Writer.TryWrite()` which is non-blocking -- if the bounded channel is full, it immediately returns false and sets an exception. While `BoundedChannelFullMode.Wait` enables backpressure for `WriteAsync`, `TryWrite` does not wait. @to11mtm notes the "tradeoff of locks vs Akka streams machinery is a wash" -- the synchronous check is intentional for performance, but means the "backpressure" is really "fail-fast + error" rather than true backpressure.

6. **SQL length limit default:** Both @to11mtm and this review question whether 5MB is the right default for `tagtable-asqueryable-insert-sql-length-limit`. @to11mtm suspects a smaller limit might be better but didn't test variations in depth. Consider cribbing from linq2db's per-provider BulkCopy limits.

7. **Duplicate projection code:** The `JournalRowIns` projection in `RunFastInsertNoEventParams` is repeated twice (mid-batch flush and final flush). Should be extracted to a helper.

8. **`Linq2DbHacks.cs` naming and organization:** Contains an empty `InstantHandleAttribute` class (JetBrains annotation substitute) and a static helper class. The naming feels WIP.

9. **INSERT OUTPUT ordering:** @to11mtm raised whether results should be ordered by `OrderingId` before the tag-join. The current `(PersistenceId, SequenceNumber)` join key is safe for correctness, but unordered output could theoretically affect downstream Query API consumers.

### Minor Issues

10. **AI-generated artifacts:** "uwu", emoji, and "CopilotNote:" markers in production code and XML doc comments. @to11mtm acknowledges these are copilot artifacts to be cleaned up.

11. **Design doc in production path:** `ChannelQueueWithBatch.md` under `Journal/Dao/`. @to11mtm confirms it will be removed before merge.

12. **Copy-paste bug:** `PostgreSqlSqlTagTableAsQueryableLargePayloadTaggedJournalPerfSpec` passes `nameof(PostgreSqlSqlTagTableLargePayloadTaggedJournalPerfSpec)` (wrong class name).

13. **XML comment typo:** Same class has `// <summary>` (single `/`) instead of `/// <summary>`.

14. **Whitespace-only change:** `PersistentRepresentationSerializer.cs` has only an indentation change.

15. **Missing newline at EOF:** `Directory.Packages.props` lost its trailing newline.

---

## Summary

This is a substantial PR that bundles 4 distinct features: linq2db 6 upgrade, AsQueryable bulk insert optimization, Channel-based write pipeline, and expanded benchmarks. The core ideas are sound -- the AsQueryable approach should meaningfully reduce round-trips for tagged events, and the Channel-based batcher addresses real problems with `Source.Queue` + `BatchWeighted` (dropped writes, poor batching under load).

The PR has active discussion between @to11mtm and @Arkatufus with **changes requested**. Key unresolved items:

- **Clone connection:** Needs deprecation path, not a hard break (per @Arkatufus)
- **Older DB compatibility:** Needs explicit verification (per @Arkatufus)
- **Channel lifetime:** Agreement needed on whether/when to close the input channel
- **Internal linq2db API usage:** Risk acknowledged but defended on performance grounds -- needs team decision
- **Unit tests for `ChannelQueueWithBatch`:** Missing entirely
- **SQL length limit default:** Author himself is uncertain about the 5MB value
- **Code cleanup:** AI artifacts, design doc, duplicate code

Given the scope, this might benefit from being split into at least 2 PRs: (1) linq2db 6 upgrade + compatibility fixes, and (2) the performance features (AsQueryable + ChannelBatchQueue + benchmarks).
