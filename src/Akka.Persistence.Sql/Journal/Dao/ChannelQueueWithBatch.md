# Design for ChannelQueueWithBatch\<TInput, TBatch\>

## Summary

A `ChannelReader<TBatch>` that wraps a `ChannelReader<TInput>` and performs eager
weighted batching on read. No background tasks, no output channels — the batching logic
lives directly in the `TryRead` and `WaitToReadAsync` overrides.

1. **Bounded queue input** — the caller creates and owns a `BoundedChannel<TInput>` with
   `BoundedChannelFullMode.Wait` for natural backpressure, and passes its `.Reader` here.
2. **Eager weighted batching** — each `TryRead` call eagerly drains available items from
   the input reader, aggregating them via user-supplied `seed`/`aggregate`/`costFunction`
   up to a per-batch weight budget. Overflow items are parked as `_pending` for the next read.

Because this class **is** a `ChannelReader<TBatch>`, it plugs directly into anything that
accepts a channel reader: `Source.ChannelReader(batcher).SelectAsync(parallelism, ...)`,
`await foreach`, `ReadAsync`, etc.

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
our `ChannelQueueWithBatch` only covers ② (batching), while ① (input channel) is
caller-owned. The output is just `this` — a `ChannelReader<TBatch>` that the caller
(or Akka Streams) can consume however it wants.

### Config Values That Map to This Abstraction

From `BaseByteArrayJournalDaoConfig`:

| Config Key     | Default | Maps To                                              |
|---------------|---------|-------------------------------------------------------|
| `buffer-size` | 5000    | Caller's `BoundedChannelOptions.Capacity`             |
| `batch-size`  | 100     | `maxWeight` (cost budget, constructor param)          |

The `parallelism` config value maps to the *downstream* `SelectAsync` — outside our scope.
The `buffer-size` is also outside this class — it's the caller's responsibility when
creating the `BoundedChannel<TInput>`.

---

## Proposed Design

### Class: `ChannelQueueWithBatch<TInput, TBatch> : ChannelReader<TBatch>`

A sealed class that **extends** `ChannelReader<TBatch>`, overriding `TryRead` and
`WaitToReadAsync`. No background tasks. No output channels. No `IDisposable`.

### Constructor Parameters

```csharp
public ChannelQueueWithBatch(
    ChannelReader<TInput> inputReader,    // Reader side of caller-owned input channel
    long maxWeight,                       // Cost budget per batch (maps to batch-size)
    Func<TInput, long> costFunction,      // Cost of a single input element
    Func<TInput, TBatch> seed,            // Create initial aggregate from first element
    Func<TBatch, TInput, TBatch> aggregate) // Fold subsequent elements into aggregate
```

The caller creates and owns the input `BoundedChannel<TInput>` externally,
retaining the `ChannelWriter<TInput>` for producing items (via `TryWrite`,
`WriteAsync`, etc.). Only the `ChannelReader<TInput>` is passed here.

### Internal State

```
┌──────────────────┐          ┌──────────────────────────────────────────┐
│  BoundedChannel   │          │ ChannelQueueWithBatch                    │
│  <TInput>         │          │ (IS a ChannelReader<TBatch>)             │
│  capacity=N       │─Reader──▶│                                          │
│  FullMode=Wait    │          │  _inputReader   (wraps the input)        │
│  (caller-owned)   │          │  _pending       (overflow item)          │
└──────────────────┘          │  _seed / _aggregate / _costFunction      │
      ▲ TryWrite /             │                                          │
      │ WriteAsync             │  TryRead()     → drains + batches        │
  [Producers]                  │  WaitToReadAsync() → pending or input    │
  (caller code)                │  Completion    → forwards from input     │
                              └──────────────────────────────────────────┘
                                       │ (this IS the ChannelReader)
                                       ▼
                                 [Consumers]
                          (SelectAsync, foreach,
                           Source.ChannelReader)
```

- **No output channel.** The class itself is the `ChannelReader<TBatch>`.
- **No background task.** Batching happens on-demand inside `TryRead`.
- **`_pending`:** A single overflow item, like `EagerBatchStage._pending`. When an
  item doesn't fit the current batch, it's parked here and becomes the seed of the
  next batch on the next `TryRead` call.
- **`Completion`:** Forwards directly from the input reader — completes when the
  caller completes the input channel.

### Write Side (Caller-Owned)

The producer API lives on the caller's `ChannelWriter<TInput>` — **not** on this class.
The caller creates the `BoundedChannel<TInput>` and writes to it directly:

```csharp
// Caller creates the input channel:
var inputChannel = Channel.CreateBounded<TInput>(new BoundedChannelOptions(bufferSize)
{
    FullMode = BoundedChannelFullMode.Wait,
    SingleReader = true,
});

// Caller writes items via the ChannelWriter:
inputChannel.Writer.TryWrite(item);          // Non-blocking, returns false if full
await inputChannel.Writer.WriteAsync(item);  // Async, backpressures when full
inputChannel.Writer.TryComplete();           // Signals no more items
inputChannel.Writer.TryComplete(exception);  // Signals error

// The batcher wraps the Reader side — and IS a ChannelReader<TBatch>:
var batcher = new ChannelQueueWithBatch<TInput, TBatch>(
    inputChannel.Reader, maxWeight, costFunc, seed, aggregate);
```

**Key behavioral difference from `Source.Queue`:** With `BoundedChannelFullMode.Wait`,
`TryWrite` returns `false` when full (non-blocking), while `WriteAsync` awaits until
space is available. This replaces `OverflowStrategy.DropNew` + `QueueOfferResult` with
simpler, more natural channel semantics.

### Read Side (This Class IS the ChannelReader)

Since `ChannelQueueWithBatch` **extends** `ChannelReader<TBatch>`, the consumer API is
just the standard `ChannelReader<T>` surface:

```csharp
batcher.TryRead(out TBatch batch);             // Non-blocking batched read
await batcher.WaitToReadAsync(ct);             // Wait for data availability
await batcher.ReadAsync(ct);                   // Async read (default impl)
await foreach (var b in batcher.ReadAllAsync(ct)) // Async enumeration (default impl)
batcher.Completion                             // Task — forwards from input reader
```

Consumers get `ReadAsync` and `ReadAllAsync` for free from the `ChannelReader<T>` base
class — they're built on our `WaitToReadAsync` + `TryRead` overrides.

### TryRead Logic

`TryRead` mirrors `EagerBatchStage` semantics — eager drain with a pending overflow slot:

```
TryRead(out TBatch batch):
  1. Get first item:
     - If _pending has a value → use it, clear _pending
     - Else inputReader.TryRead(out firstItem)
     - If neither → return false (no data available)

  2. batch = seed(firstItem)
     remaining = maxWeight - costFunction(firstItem)

  3. Eager drain loop:
     while inputReader.TryRead(out nextItem):
       cost = costFunction(nextItem)
       if cost > remaining:
         _pending = nextItem   ← park overflow for next call
         break
       batch = aggregate(batch, nextItem)
       remaining -= cost

  4. return true (batch contains the aggregated result)
```

### WaitToReadAsync Logic

```
WaitToReadAsync(ct):
  - If _pending has a value → return true immediately
  - Else → return inputReader.WaitToReadAsync(ct)
```

**Why this is simpler than a background loop:**
- No `Task.Factory.StartNew` / `TaskCreationOptions.LongRunning`
- No `UnboundedChannel<TBatch>` output buffer
- No `IDisposable` / cleanup concerns
- No `outputWriter.TryWrite` / `outputWriter.TryComplete` plumbing
- Batching is demand-driven (happens when consumer pulls) not supply-driven
- The `_pending` field handles overflow identically to `EagerBatchStage`

**Why this matches `EagerBatchStage` semantics:**
- `EagerBatchStage.OnPush()` keeps pulling upstream while there's budget (lines 112-114).
  Our `TryRead` does the same via the drain loop — it greedily takes everything available.
- When the batch is full (`_left < cost`), `EagerBatchStage` parks the element as
  `_pending` (lines 88-92). Our `TryRead` does exactly the same.
- `EagerBatchStage.OnPull()` flushes whatever has accumulated (line 158).
  Our `TryRead` returns the accumulated batch — same result.

---

## Akka Streams Composability

The key integration point: downstream Akka Streams consumption via `Source.ChannelReader`.

Since `ChannelQueueWithBatch` **is** a `ChannelReader<TBatch>`, it passes directly to
`Source.ChannelReader()`. The future migration path in `BaseByteArrayJournalDao`:

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
_inputChannel = Channel.CreateBounded<WriteQueueEntry>(new BoundedChannelOptions(BufferSize)
{
    FullMode = BoundedChannelFullMode.Wait,
    SingleReader = true,
});

_batcher = new ChannelQueueWithBatch<WriteQueueEntry, WriteQueueSet>(
    _inputChannel.Reader,
    maxWeight: BatchSize,
    costFunction: entry => entry.Rows.Count,
    seed: entry => new WriteQueueSet(...),
    aggregate: (set, entry) => ...,);

// The batcher IS a ChannelReader — pass it directly to Source.ChannelReader
Source.ChannelReader(_batcher)
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
if (!_inputChannel.Writer.TryWrite(new WriteQueueEntry(promise, xs, ct)))
{
    // Check whether the batcher faulted/closed vs the input channel simply being full.
    // This mirrors the original Dropped vs Failure vs QueueClosed distinction.
    var ex = _batcher.Completion.Exception;
    if (ex is not null)
        promise.TrySetException(new Exception("Failed to write journal row batch", ex));
    else if (_batcher.Completion.IsCompleted)
        promise.TrySetException(new Exception(
            "Failed to enqueue journal row batch write, the queue was closed."));
    else
        promise.TrySetException(new Exception(
            $"Failed to enqueue journal row batch write, the queue buffer was full ({BufferSize} elements)"));
}
```

`TryWrite` on the caller's `ChannelWriter` returns `false` for three reasons —
matching the original `QueueOfferResult` cases:

| `TryWrite` = false because…         | Original equivalent        | How we detect it                         |
|--------------------------------------|----------------------------|------------------------------------------|
| Channel is at capacity               | `QueueOfferResult.Dropped` | `Completion` is not completed, no exception |
| Channel was completed with an error  | `QueueOfferResult.Failure` | `Completion.Exception` is non-null       |
| Channel was completed normally       | `QueueOfferResult.QueueClosed` | `Completion.IsCompleted` and no exception |

The `Completion` property (forwarded from the input reader) lets callers inspect
the reason when `TryWrite` fails.

---

## Error Handling

- **`costFunction` / `seed` / `aggregate` exceptions:** These propagate naturally out
  of `TryRead` to the consumer. The consumer (e.g. `RestartingDecider` in Akka Streams,
  or a `try/catch` in `await foreach`) handles recovery.
- **Input channel completion:** `Completion` forwards from the input reader. When the
  caller completes the input channel, `WaitToReadAsync` returns `false` (after any
  pending item is consumed), and consumers see a completed reader.
- **Input channel error:** If the caller completes the input channel with an exception,
  `Completion` faults accordingly, and `WaitToReadAsync` / `TryRead` surface the error
  per standard `ChannelReader` semantics.

---

## Test Plan

Unit tests for `ChannelQueueWithBatch` should be standalone (no Akka dependency needed).
Tests create a `BoundedChannel<TInput>`, pass its `.Reader` to the batcher, write via
the `.Writer`, and read batched output from the batcher (which IS the `ChannelReader`):

| Test Case | What It Verifies |
|-----------|-----------------|
| **Single item passthrough** | One write → one `TryRead` yields a batch seeded from that single item |
| **Multiple items within weight budget** | N items where `sum(cost) ≤ maxWeight` → aggregated into a single batch |
| **Items exceeding weight budget** | Items with `sum(cost) > maxWeight` → split across multiple batches at the right boundaries |
| **Weighted cost function** | Items with varying costs respect the weight budget, not just count |
| **Backpressure when full** | `capacity=1`, write 2 items — second `WriteAsync` on the channel blocks until the first is consumed |
| **TryWrite when full** | `capacity=1`, `TryWrite` on the channel returns false when at capacity |
| **Complete flushes partial batch** | Write items, call `writer.TryComplete()` — partial aggregate is available via `TryRead`, then `WaitToReadAsync` returns false |
| **Complete with error** | `writer.TryComplete(exception)` → `Completion` faults with that exception |
| **Cancellation stops WaitToReadAsync** | Cancel the token passed to `WaitToReadAsync` — throws `OperationCanceledException` |
| **Empty complete** | Call `writer.TryComplete()` with no items written → `WaitToReadAsync` returns false immediately |
| **Overflow item seeds next batch** | An item that doesn't fit the current batch becomes the seed of the next batch (not lost) |

---

## Open Questions / Future Considerations

1. **Output channel bounded vs unbounded?** No longer applicable — there is no output
   channel. The class IS the `ChannelReader<TBatch>`.

2. **Relationship to `EagerBatchStage`?** This abstraction replicates the eager-drain
   semantics of `EagerBatchStage` but outside of Akka Streams. Long term, if we move fully
   to channels, `EagerBatchStage` could remain for Akka Streams-only use cases while
   `ChannelQueueWithBatch` covers the "I just need a batched channel reader" case.

---

## Implementation Checklist

### Phase 1: Abstraction (new files only, no existing code changes)

- [x] `src/Akka.Persistence.Sql/Utility/ChannelQueueWithBatch.cs`
  - [x] Sealed class extending `ChannelReader<TBatch>`
  - [x] Constructor: `inputReader`, `maxWeight`, `costFunction`, `seed`, `aggregate`
  - [x] `_gate` lock object for thread-safe `_pending` access
  - [x] `_pending` overflow field (mirrors `EagerBatchStage._pending`)
  - [x] `override TryRead` — eager drain + aggregate + overflow parking (under lock)
  - [x] `override WaitToReadAsync` — pending check (under lock) + delegate to input
  - [x] `override Completion` — forwards from input reader
  - [x] Xmldoc on all public members

- [ ] `src/Akka.Persistence.Sql.Tests/Internal/ChannelQueueWithBatchSpec.cs`
  - [ ] Single item passthrough
  - [ ] Multiple items within weight budget → single batch
  - [ ] Items exceeding weight budget → split across batches
  - [ ] Weighted cost function respected
  - [ ] Backpressure when full (`capacity=1`, `WriteAsync` blocks on input channel)
  - [ ] `TryWrite` returns false when input channel full
  - [ ] `writer.TryComplete()` flushes partial batch then reader completes
  - [ ] `writer.TryComplete(exception)` faults reader
  - [ ] Cancellation stops `WaitToReadAsync`
  - [ ] Empty complete → `WaitToReadAsync` returns false, no batches
  - [ ] Overflow item seeds next batch (not lost)

### Phase 2: Integration (swap into `BaseByteArrayJournalDao`)

- [ ] `BaseByteArrayJournalDao` — replace `Source.Queue` + `BatchWeighted` pipeline
  - [ ] Add field: `Channel<WriteQueueEntry> _inputChannel` (BoundedChannel, FullMode=Wait, SingleReader=true)
  - [ ] Add field: `ChannelQueueWithBatch<WriteQueueEntry, WriteQueueSet> _batcher`
  - [ ] Replace `WriteQueue` initialization (lines ~71–103) with:
    - [ ] Create `_inputChannel = Channel.CreateBounded<WriteQueueEntry>(...)`
    - [ ] Create `_batcher = new ChannelQueueWithBatch<>(_inputChannel.Reader, ...)`
    - [ ] Wire `Source.ChannelReader(_batcher).SelectAsync(Parallelism, handler)...`
  - [ ] Replace `ISourceQueueWithComplete<WriteQueueEntry> WriteQueue` field with `_inputChannel` + `_batcher`
  - [ ] Update `QueueWriteJournalRows()`:
    - [ ] Replace `WriteQueue.OfferAsync(entry)` + `QueueOfferResult` switch with `_inputChannel.Writer.TryWrite(entry)`
    - [ ] On `TryWrite` failure: check `_batcher.Completion` to distinguish full/faulted/closed
  - [ ] Verify `RestartingDecider` supervision still applies via `.AddAttributes()` on the stream

- [ ] `ByteArrayJournalDao` — confirm no changes needed (inherits from `BaseByteArrayJournalDao`)

- [ ] Remove or deprecate unused types (if no longer needed after swap):
  - [ ] Evaluate whether `WriteQueueEntry` / `WriteQueueSet` need changes
  - [ ] Evaluate whether `EagerBatchStage` is still used elsewhere

### Validation

- [ ] `dotnet build` succeeds for both projects
- [ ] All new unit tests pass via `dotnet test`
- [ ] Existing `JournalSpec` / `JournalPerfSpec` tests pass for all providers
- [ ] No existing tests broken

