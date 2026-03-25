# ✨ Replace `Source.Queue` + `BatchWeighted` with channel-based batching & optimize bulk inserts

## Summary

This PR refactors the journal write pipeline in `BaseByteArrayJournalDao` in two main areas:

1. **Channel-based batching** — replaces the Akka Streams `Source.Queue` + `BatchWeighted` pipeline with a `System.Threading.Channels` bounded channel and a new `ChannelQueueWithBatch` abstraction, eliminating silent write drops and providing natural backpressure.
2. **Bulk insert optimizations** — switches `RunFastInsertNoEventParams` to pooled arrays via `ArrayPool<JournalRow>`, replaces LINQ `Join` + `SelectMany` tag-row assembly with a direct `foreach` loop, and introduces `InsertWithOutputListAsync` to avoid LinqToDB's `IAsyncEnumerable` overhead.

Additionally, two new reusable utility classes are introduced (`ChannelQueueWithBatch` and `EagerBatchStage`) along with a design document.

---

## Changed Files

### `BaseByteArrayJournalDao.cs` — Pipeline & insert refactors

**Write pipeline replacement:**

- Removed the `ISourceQueueWithComplete<WriteQueueEntry> WriteQueue` field.
- Added two new private fields:
  - `Channel<WriteQueueEntry> _inputChannel` — bounded channel with `BoundedChannelFullMode.Wait` (replaces `Source.Queue` with `OverflowStrategy.DropNew`).
  - `ChannelQueueWithBatch<WriteQueueEntry, WriteQueueSet> _batcher` — weighted batcher wrapping the channel's reader, aggregating `WriteQueueEntry` items into `WriteQueueSet` batches up to the configured `BatchSize` cost budget.
- Constructor: the old `Source.Queue(...).BatchWeighted(...).SelectAsync(...)` chain is replaced with `Channel.CreateBounded` → `new ChannelQueueWithBatch(...)` → `Source.ChannelReader(_batcher).SelectAsync(...)`. The `SelectAsync` handler, `RestartingDecider` supervision, and `Sink.Ignore` are preserved identically.
- Materialization simplified from `.ToMaterialized(Sink.Ignore, Keep.Left)` to `.To(Sink.Ignore).Run(Materializer)` — we no longer need the materialized queue reference since we own the channel directly.

**`QueueWriteJournalRows` simplification:**

- Replaced `WriteQueue.OfferAsync(entry)` and its 4-case `QueueOfferResult` switch (`Enqueued` / `Dropped` / `Failure` / `QueueClosed`) with a single `_inputChannel.Writer.TryWrite(entry)` call.
- On `TryWrite` failure, the method inspects `_batcher.Completion` to distinguish channel-full vs faulted vs closed — mapping to the same error messages as the original switch cases.

**`RunFastInsertNoEventParams` memory optimizations:**

- Switched from `new List<JournalRow>(rowLimit)` to `ArrayPool<JournalRow>.Shared.Rent(rowLimit)` — avoids per-batch heap allocations for the row buffer. The rented array is properly cleared (with `#if NET6_0_OR_GREATER` conditional for the `Array.Clear` overload) and returned to the pool after use.
- Changed `insertList.Select(...)` to `insertList.Take(currRows).Select(...)` to correctly slice the rented array to only the populated portion.
- Added `thisInsTagSize` tracking to preallocate the tag row list with the correct capacity in downstream `InsertJournalEntriesWithTags`.
- Adjusted `rowLimit` to `Math.Max(xs.Length, BatchSize)` so rented arrays accommodate batches larger than the configured size.

**`InsertJournalEntriesWithTags` refactors:**

- Switched from `InsertWithOutputAsync(...).ToListAsync(token)` to the new `InsertWithOutputListAsync(...)` extension, which returns `Task<List<T>>` directly and avoids LinqToDB's intermediate `IAsyncEnumerable` allocation.
- Replaced the LINQ `Join` + `SelectMany` tag-row assembly with a direct `foreach` loop over inserted rows, using `Dictionary.TryGetValue` + `AddRange` to build the flat `List<JournalTagRow>` — eliminating the intermediate `IEnumerable<IEnumerable<>>` and `.SelectMany(t => t)` flatten.
- Added `thisInsTagSize` parameter to preallocate the tag list at the right capacity.

---

### `ChannelQueueWithBatch.cs` *(new file)* — Channel-based weighted batcher

A `sealed class` extending `ChannelReader<TBatch>` that wraps a `ChannelReader<TInput>` and performs eager weighted batching purely on read:

- `TryRead` eagerly drains available items from the input reader up to a `maxWeight` cost budget, aggregating them via caller-supplied `seed`/`aggregate` delegates. Overflow items that don't fit are parked in a `_pending` field for the next call — mirroring `EagerBatchStage._pending` semantics.
- `WaitToReadAsync` checks for a pending overflow item first (instant `true`), then delegates to the input reader.
- `Completion` forwards directly from the input reader.
- Thread safety via a simple `lock(_gate)` over fully synchronous critical sections (no async inside the lock).
- No background tasks, no output channel, no `IDisposable` — the class *is* the `ChannelReader<TBatch>` and plugs directly into `Source.ChannelReader(batcher)`.

---

### `EagerBatch.cs` *(new file)* — Aggressive Akka Streams batch stage

A `GraphStage<FlowShape<TIn, TOut>>` that provides more aggressive batching than the built-in `Batch`/`BatchWeighted`:

- Standard `Batch.OnPush` flushes partial aggregates whenever the outlet is available — so when followed by fast-pulling stages like `SelectAsync`, it often produces single-element "batches". `EagerBatchStage.OnPush` only flushes when the batch is actually **full** (pending element set), otherwise keeps pulling upstream.
- Emits on: batch full, downstream explicit pull (`OnPull`), or upstream completion (`OnUpstreamFinish`).
- Includes full supervision `Decider` support (Stop / Restart / Resume).
- Provides `EagerBatch` and `EagerBatchWeighted` extension methods for `Source<T>`, `Flow<T>`, and `IFlow<T>` — drop-in replacements for `.Batch()` / `.BatchWeighted()`.

---

### `Linq2DbHacks.cs` *(new file)* — `InsertWithOutputListAsync` helper

An extension method that combines LinqToDB's `InsertWithOutput` + `ToListAsync` into a single `Task<List<TOutput>>` call:

- LinqToDB's built-in `InsertWithOutputAsync` returns `IAsyncEnumerable<T>` with some synchronous-ish internal behavior. This helper constructs the expression tree directly and calls `.ToListAsync()` on the resulting `IQueryable<TOutput>`, running the query more efficiently as a single round-trip.
- Used by `InsertJournalEntriesWithTags` to replace the previous `.InsertWithOutputAsync(...).ToListAsync(token)` chain.

---

### `ChannelQueueWithBatch.md` *(new file)* — Design document

Comprehensive design document covering:
- Current architecture analysis (`Source.Queue` + `BatchWeighted` + `SelectAsync` pipeline)
- Pain points with `OverflowStrategy.DropNew` and standard `BatchWeighted` flushing behavior
- Proposed `ChannelQueueWithBatch` design with ASCII architecture diagram
- `TryRead` / `WaitToReadAsync` logic walkthroughs
- Akka Streams composability via `Source.ChannelReader`
- Error handling semantics
- Test plan with 11 specific test cases
- Implementation checklist tracking Phase 1 (abstraction) and Phase 2 (integration)

---

## Behavioral Changes

| Aspect | Before | After |
|--------|--------|-------|
| **Backpressure** | `OverflowStrategy.DropNew` — silently drops writes when buffer is full | `BoundedChannelFullMode.Wait` — `TryWrite` returns `false`, never silently drops |
| **Batch assembly** | Akka Streams `BatchWeighted` stage | `ChannelQueueWithBatch` eager drain on `TryRead` |
| **Queue offering** | `WriteQueue.OfferAsync()` → 4-case `QueueOfferResult` switch | `_inputChannel.Writer.TryWrite()` → `_batcher.Completion` inspection |
| **Row buffer allocation** | `new List<JournalRow>(rowLimit)` per batch | `ArrayPool<JournalRow>.Shared.Rent(rowLimit)` — pooled, reused |
| **Tag row assembly** | LINQ `Join` + `SelectMany` → `IEnumerable<IEnumerable<>>` | Direct `foreach` + `TryGetValue` + `AddRange` → flat `List<JournalTagRow>` |
| **Insert+output** | `InsertWithOutputAsync(...).ToListAsync()` | `InsertWithOutputListAsync(...)` — single expression tree query |
| **Stream materialization** | `.ToMaterialized(Sink.Ignore, Keep.Left)` — materializes `ISourceQueueWithComplete` | `.To(Sink.Ignore).Run()` — no materialized value needed |
