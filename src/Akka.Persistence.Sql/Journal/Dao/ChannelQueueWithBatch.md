# Design for ChannelQueueWithBatch\<TInput, TBatch\>

## Summary

A standalone, generic abstraction backed by `System.Threading.Channels` that merges:

1. **Bounded queue input** — like `Source.Queue<T>(capacity, OverflowStrategy)`, but using
   `BoundedChannel<TInput>` with `BoundedChannelFullMode.Wait` for natural backpressure.
2. **Eager weighted batching** — like `BatchWeighted` / our custom `EagerBatchStage<TIn, TOut>`,
   but implemented as an internal consumer loop that eagerly drains available items from the
   `ChannelReader` and aggregates them via user-supplied `seed`/`aggregate`/`costFunction`.

The output is a `ChannelReader<TBatch>` — keeping the abstraction generic and composable.
Consumers decide how to process batches: Akka Streams can use `Source.ChannelReader(reader)`
piped into `.SelectAsync(parallelism, ...)`, or plain async consumers can use `await foreach`.

> **Scope:** This is a *new file* abstraction only. It does NOT replace the existing
> `Source.Queue` + `BatchWeighted` pipeline in `BaseByteArrayJournalDao` — that swap
> is a separate future step.

---

## Current Architecture (What We're Modeling)

In `BaseByteArrayJournalDao`, the write pipeline is built as an Akka Streams graph
that is materialized once during construction:

```
Source.Queue<WriteQueueEntry>(BufferSize, OverflowStrategy.DropNew)   // ① Input
  .BatchWeighted(BatchSize, costFunc, seed, aggregate)                // ② Batching
  .SelectAsync(Parallelism, handler)                                  // ③ Processing
  .AddAttributes(RestartingDecider)                                   // ④ Supervision
  .ToMaterialized(Sink.Ignore, Keep.Left)                             // ⑤ Materialize
  .Run(Materializer)                                                  // → ISourceQueueWithComplete<T>
```

### ① Source.Queue — The Input Side

- Creates a bounded buffer of `BufferSize` (default 5000) elements.
- Returns an `ISourceQueueWithComplete<WriteQueueEntry>` materialized value.
- Callers use `OfferAsync(entry)` which returns a `QueueOfferResult` discriminated union:
  `Enqueued`, `Dropped`, `Failure`, or `QueueClosed`.
- Uses `OverflowStrategy.DropNew` — when the buffer is full, the newest offer is **dropped**
  (not backpressured), and the caller must handle the `Dropped` result.
- See `QueueWriteJournalRows()` (line ~280) for the offer + error-handling boilerplate.

**Pain point:** The `DropNew` + `QueueOfferResult` pattern requires a verbose `switch`
statement at every call site, and dropping writes silently is risky for persistence.

### ② BatchWeighted — The Batching Stage

- Accumulates `WriteQueueEntry` items into a `WriteQueueSet` aggregate.
- Uses `costFunc: entry => entry.Rows.Count` — each entry's cost is its row count.
- `seed`: wraps the first entry into a new `WriteQueueSet` with single-element
  `ImmutableList`s for `Tcs` and `CancellationTokens`.
- `aggregate`: folds subsequent entries by `.Add()`-ing to the immutable lists and
  `.Concat()`-ing the `Seq<JournalRow>` rows.
- `maxWeight` = `BatchSize` (default 100) — the batch emits once total row count hits this.

**Pain point:** Standard `BatchWeighted` flushes partial batches eagerly when downstream
is available (during `OnPush`). This means when `SelectAsync(Parallelism)` pulls fast,
we often get single-element "batches" — defeating the purpose of batching. This is exactly
why we built `EagerBatchStage` in `Utility/EagerBatch.cs`, which only flushes when the
batch is actually full or downstream explicitly pulls.

### ③–⑤ SelectAsync + Supervision + Sink (NOT in scope)

The downstream `SelectAsync(Parallelism, handler)` processes each `WriteQueueSet` batch,
resolving all `TaskCompletionSource` promises on success or failure. The `RestartingDecider`
ensures the stream survives exceptions. **These stages are NOT part of this abstraction** —
our `ChannelQueueWithBatch` only covers ① + ②, exposing a `ChannelReader<TBatch>` that
the caller (or Akka Streams) can consume however it wants.

### Config Values That Map to This Abstraction

From `BaseByteArrayJournalDaoConfig`:

| Config Key     | Default | Maps To                         |
|---------------|---------|----------------------------------|
| `buffer-size` | 5000    | `capacity` (BoundedChannel size) |
| `batch-size`  | 100     | `maxWeight` (cost budget)        |

The `parallelism` config value maps to the *downstream* `SelectAsync` — outside our scope.

---

## Proposed Design

### Class: `ChannelQueueWithBatch<TInput, TBatch>`

A single sealed class in a new file (e.g. `Utility/ChannelQueueWithBatch.cs` or
`Streams/ChannelQueueWithBatch.cs`). Implements `IDisposable` for cleanup.

### Constructor Parameters

```csharp
public ChannelQueueWithBatch(
    int capacity,                         // BoundedChannel size (maps to buffer-size)
    long maxWeight,                       // Cost budget per batch (maps to batch-size)
    Func<TInput, long> costFunction,      // Cost of a single input element
    Func<TInput, TBatch> seed,            // Create initial aggregate from first element
    Func<TBatch, TInput, TBatch> aggregate, // Fold subsequent elements into aggregate
    CancellationToken shutdownToken = default)
```

### Internal State

```
┌─────────────────────────────────────────────────────────────┐
│                   ChannelQueueWithBatch                      │
│                                                             │
│  ┌──────────────────┐    ┌──────────────┐    ┌───────────┐  │
│  │  BoundedChannel   │───▶│ Batch Loop   │───▶│ Unbounded │  │
│  │  <TInput>         │    │ (Task)       │    │ Channel   │  │
│  │  capacity=N       │    │              │    │ <TBatch>  │  │
│  │  FullMode=Wait    │    │ seed/agg/    │    │           │  │
│  │                   │    │ costFunc     │    │           │  │
│  └──────────────────┘    └──────────────┘    └───────────┘  │
│        ▲ WriteAsync()                          │ .Reader    │
│        │                                       ▼            │
│    [Producers]                           [Consumers]        │
│                                   (SelectAsync, foreach,    │
│                                    Source.ChannelReader)     │
└─────────────────────────────────────────────────────────────┘
```

- **Input channel:** `Channel.CreateBounded<TInput>(new BoundedChannelOptions(capacity) 
  { FullMode = BoundedChannelFullMode.Wait, SingleReader = true })`.
  `SingleReader = true` because only our internal batch loop reads from it.
- **Output channel:** `Channel.CreateUnbounded<TBatch>(new UnboundedChannelOptions
  { SingleWriter = true })`.
  `SingleWriter = true` because only our internal batch loop writes to it.
  Unbounded is safe here because the input channel already bounds inflow — the batch
  loop can only produce batches as fast as input arrives, and each batch *reduces* count.
- **Batch loop:** A long-running `Task` started in the constructor via
  `Task.Factory.StartNew(..., TaskCreationOptions.LongRunning)` to avoid threadpool
  starvation for this always-on loop.

### Write Side (Producer API)

```csharp
/// Writes an item to the input channel, waiting if the channel is full.
/// This is the replacement for ISourceQueueWithComplete.OfferAsync().
/// Instead of returning QueueOfferResult, it naturally backpressures via await.
public ValueTask WriteAsync(TInput item, CancellationToken cancellationToken = default)

/// Tries to write synchronously without waiting. Returns false if full,
/// completed, or faulted. Check Completion to distinguish the reason.
public bool TryWrite(TInput item)

/// Signals that no more items will be written. The batch loop will flush
/// any partial aggregate and complete the output Reader.
public void Complete()

/// Signals completion with an error. The output Reader will fault.
public void Complete(Exception error)

/// A Task that completes when the batch loop finishes and the output channel
/// closes. If the loop faulted, this Task's Exception carries the cause.
/// Callers can inspect this after TryWrite returns false to distinguish
/// "full" from "faulted" from "closed".
public Task Completion { get; }
```

**Key behavioral difference from `Source.Queue`:** Instead of `DropNew` + checking a
`QueueOfferResult`, `WriteAsync` simply `await`s until there's room. This gives natural
backpressure — the caller slows down instead of losing data. For the persistence use case
this is strictly better: we never want to *drop* journal writes.

### Read Side (Consumer API)

```csharp
/// The output channel reader. Each read yields one aggregated TBatch.
/// Consumers can use this with:
///   - Source.ChannelReader(queue.Reader).SelectAsync(parallelism, handler)
///   - await foreach (var batch in queue.Reader.ReadAllAsync(ct))
///   - await queue.Reader.ReadAsync(ct)
public ChannelReader<TBatch> Reader { get; }
```

No processing logic — the consumer decides what to do with batches.

### Batching Loop (Internal)

The loop mirrors the semantics of `EagerBatchStage` but uses `ChannelReader` draining
instead of Akka Streams push/pull:

```
LOOP:
  1. await inputReader.WaitToReadAsync(shutdownToken)
     → if returns false (channel completed), goto DONE

  2. inputReader.TryRead(out firstItem)
     → must succeed since WaitToReadAsync returned true
     batch = seed(firstItem)
     remaining = maxWeight - costFunction(firstItem)

  3. DRAIN loop (eager aggregation):
     while remaining > 0 AND inputReader.TryRead(out nextItem):
       cost = costFunction(nextItem)
       if cost > remaining:
         // This item won't fit — we need to emit current batch
         // then start a new batch with this item as the seed
         outputWriter.TryWrite(batch)
         batch = seed(nextItem)
         remaining = maxWeight - cost
       else:
         batch = aggregate(batch, nextItem)
         remaining -= cost

  4. outputWriter.TryWrite(batch)
     → TryWrite on unbounded channel always succeeds

  5. goto LOOP

DONE:
  // Input channel completed — output channel completes too
  outputWriter.Complete()
```

**Why this matches `EagerBatchStage` semantics:**
- `EagerBatchStage.OnPush()` keeps pulling upstream while there's budget (lines 112-114).
  Our drain loop does the same via `TryRead` — it greedily takes everything available.
- `EagerBatchStage.OnPull()` flushes whatever has accumulated so far (line 158).
  Our loop writes to the output channel after draining, so the consumer sees complete batches.
- When the batch is full (`_left < cost`), `EagerBatchStage` parks the element as `_pending`
  and flushes (lines 88-92, 107-110). Our loop emits the batch and seeds a new one with
  the overflow item — same result, no parking needed because we control the loop.

**Why `TryRead` after `WaitToReadAsync` is the right pattern:**
- `WaitToReadAsync` blocks (async) until at least one item is available — this is the
  "wait for work" part, equivalent to the initial `Pull` in a graph stage.
- `TryRead` in a loop is non-blocking and drains everything currently queued — this is
  the "eager batch" part. When `TryRead` returns false, we've consumed all *currently
  available* items, so we emit the batch and go back to waiting.
- This naturally adapts to load: under low load, batches are small (1 item); under high
  load, batches fill up to `maxWeight` because items accumulate while we're processing.

---

## Akka Streams Composability

The key integration point: downstream Akka Streams consumption via `Source.ChannelReader`.

In Akka.NET 1.5.x, `Source.ChannelReader<T>(channelReader)` wraps a
`System.Threading.Channels.ChannelReader<T>` into an Akka Streams `Source<T, NotUsed>`.
This means the future migration path in `BaseByteArrayJournalDao` would look like:

```csharp
// Before (current):
WriteQueue = Source
    .Queue<WriteQueueEntry>(BufferSize, OverflowStrategy.DropNew)
    .BatchWeighted(BatchSize, costFunc, seed, aggregate)
    .SelectAsync(Parallelism, handler)
    .AddAttributes(RestartingDecider)
    .ToMaterialized(Sink.Ignore, Keep.Left)
    .Run(Materializer);

// After (future, using this abstraction):
_channelQueue = new ChannelQueueWithBatch<WriteQueueEntry, WriteQueueSet>(
    capacity: BufferSize,
    maxWeight: BatchSize,
    costFunction: entry => entry.Rows.Count,
    seed: entry => new WriteQueueSet(...),
    aggregate: (set, entry) => ...,
    shutdownToken: shutdownToken);

// The batched output feeds into SelectAsync for processing
Source.ChannelReader(_channelQueue.Reader)
    .SelectAsync(Parallelism, handler)
    .AddAttributes(RestartingDecider)
    .ToMaterialized(Sink.Ignore, Keep.None)
    .Run(Materializer);
```

The `QueueWriteJournalRows` method also simplifies — no more `QueueOfferResult` switch:

```csharp
// Before:
var result = await WriteQueue.OfferAsync(new WriteQueueEntry(promise, xs, ct));
switch (result) { /* 4 cases of Enqueued/Dropped/Failure/QueueClosed handling */ }

// After:
if (!_channelQueue.TryWrite(new WriteQueueEntry(promise, xs, ct)))
{
    // Check whether the channel faulted/closed vs simply being full.
    // This mirrors the original Dropped vs Failure vs QueueClosed distinction.
    var ex = _channelQueue.Completion.Exception;
    if (ex is not null)
        promise.TrySetException(new Exception("Failed to write journal row batch", ex));
    else if (_channelQueue.Completion.IsCompleted)
        promise.TrySetException(new Exception(
            "Failed to enqueue journal row batch write, the queue was closed."));
    else
        promise.TrySetException(new Exception(
            $"Failed to enqueue journal row batch write, the queue buffer was full ({BufferSize} elements)"));
}
```

`TryWrite` returns `false` for three reasons — matching the original `QueueOfferResult` cases:

| `TryWrite` = false because…         | Original equivalent        | How we detect it                         |
|--------------------------------------|----------------------------|------------------------------------------|
| Channel is at capacity               | `QueueOfferResult.Dropped` | `Completion` is not completed, no exception |
| Channel was completed with an error  | `QueueOfferResult.Failure` | `Completion.Exception` is non-null       |
| Channel was completed normally       | `QueueOfferResult.QueueClosed` | `Completion.IsCompleted` and no exception |

To support this, `ChannelQueueWithBatch` should expose a `Task Completion` property
(forwarding the output channel's `Reader.Completion`) so callers can inspect the reason.

---

## Error Handling

- **`costFunction` / `seed` / `aggregate` exceptions:** The batch loop should catch
  exceptions from user-supplied delegates. For the initial implementation, let them
  propagate to fault the output channel — the consumer (e.g. `RestartingDecider` in Akka
  Streams) handles recovery. We can add a configurable error callback later if needed.
- **Cancellation:** The `shutdownToken` cancels the `WaitToReadAsync`, causing the loop
  to exit gracefully. Any partial aggregate is flushed before completing the output channel.
- **Disposal:** `Dispose()` calls `Complete()` on the input channel if not already done,
  and suppresses the batch loop task.

---

## Test Plan

Unit tests for `ChannelQueueWithBatch` should be standalone (no Akka dependency needed):

| Test Case | What It Verifies |
|-----------|-----------------|
| **Single item passthrough** | One `WriteAsync` → one `ReadAsync` yields a batch seeded from that single item |
| **Multiple items within weight budget** | N items where `sum(cost) ≤ maxWeight` → aggregated into a single batch |
| **Items exceeding weight budget** | Items with `sum(cost) > maxWeight` → split across multiple batches at the right boundaries |
| **Weighted cost function** | Items with varying costs respect the weight budget, not just count |
| **Backpressure when full** | `capacity=1`, write 2 items — second `WriteAsync` blocks until the first is consumed |
| **TryWrite when full** | `capacity=1`, `TryWrite` returns false when channel is at capacity |
| **Complete flushes partial batch** | Write items, call `Complete()` — partial aggregate is emitted, then `Reader` completes |
| **Complete with error** | `Complete(exception)` → `Reader` faults with that exception |
| **Cancellation stops loop** | Cancel the `shutdownToken` — loop exits, output channel completes |
| **Empty complete** | Call `Complete()` with no items written → `Reader` completes immediately, no batches emitted |
| **Overflow item seeds next batch** | An item that doesn't fit the current batch becomes the seed of the next batch (not lost) |

---

## Open Questions / Future Considerations

1. **Output channel bounded vs unbounded?** Currently proposed as unbounded. The input
   channel already bounds inflow, and the batch loop *reduces* item count (N inputs → 1
   batch), so unbounded output shouldn't grow without bound in practice. If downstream
   consumption is very slow we could revisit — but that same problem exists today with
   the Akka Streams pipeline.

2. **Relationship to `EagerBatchStage`?** This abstraction replicates the eager-drain
   semantics of `EagerBatchStage` but outside of Akka Streams. Long term, if we move fully
   to channels, `EagerBatchStage` could remain for Akka Streams-only use cases while
   `ChannelQueueWithBatch` covers the "I just need a batched queue" case.

3. **File location?** Proposed: `Akka.Persistence.Sql/Utility/ChannelQueueWithBatch.cs`
   (alongside `EagerBatch.cs`), with tests in
   `Akka.Persistence.Sql.Tests/Internal/ChannelQueueWithBatchSpec.cs`.
