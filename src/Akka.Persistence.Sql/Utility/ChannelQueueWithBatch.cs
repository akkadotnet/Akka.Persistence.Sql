// -----------------------------------------------------------------------
//  <copyright file="ChannelQueueWithBatch.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Akka.Persistence.Sql.Utility
{
    /// <summary>
    /// A <see cref="ChannelReader{TBatch}"/> that wraps a <see cref="ChannelReader{TInput}"/>
    /// and performs eager weighted batching on read. Each call to <see cref="TryRead"/>
    /// drains as many items as possible from the input reader (up to the weight budget),
    /// aggregates them via user-supplied <c>seed</c>/<c>aggregate</c> delegates, and
    /// returns the completed batch.
    ///
    /// <para>
    /// This mirrors <see cref="Akka.Streams.Dsl.EagerBatchStage{TIn,TOut}"/> semantics
    /// but operates purely on <c>System.Threading.Channels</c>, making it composable:
    /// <list type="bullet">
    ///   <item><c>Source.ChannelReader(batcher).SelectAsync(parallelism, handler)</c></item>
    ///   <item><c>await foreach (var batch in batcher.ReadAllAsync(ct))</c></item>
    ///   <item><c>await batcher.ReadAsync(ct)</c></item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// The caller creates and owns the input <see cref="Channel{TInput}"/>
    /// (typically a <c>BoundedChannel</c> with <c>FullMode = Wait</c>) and passes
    /// its <see cref="ChannelReader{TInput}"/> here. The caller retains the
    /// <c>ChannelWriter&lt;TInput&gt;</c> for producing items.
    /// </para>
    ///
    /// <para>
    /// <b>CopilotNote:</b> This class covers only the <c>BatchWeighted</c> portion.
    /// The input channel (replacing <c>Source.Queue</c>) and downstream processing
    /// (replacing <c>SelectAsync</c>) are the caller's responsibility.
    /// See <c>ChannelQueueWithBatch.md</c> for full design.
    /// </para>
    /// </summary>
    /// <typeparam name="TInput">The type of individual items read from the input channel.</typeparam>
    /// <typeparam name="TBatch">The aggregated batch type emitted to consumers.</typeparam>
    public sealed class ChannelQueueWithBatch<TInput, TBatch> : ChannelReader<TBatch>
    {
        private readonly ChannelReader<TInput> _inputReader;
        private readonly long _maxWeight;
        private readonly Func<TInput, long> _costFunction;
        private readonly Func<TInput, TBatch> _seed;
        private readonly Func<TBatch, TInput, TBatch> _aggregate;

        /// <summary>
        /// Guards access to <see cref="_pending"/> across <see cref="TryRead"/> and
        /// <see cref="WaitToReadAsync"/>. The critical sections are fully synchronous,
        /// so a simple lock is safe and cheap. 🔒
        /// </summary>
        private readonly object _gate = new();

        /// <summary>
        /// Overflow item that didn't fit in the previous batch, parked for the next
        /// <see cref="TryRead"/>. This mirrors the <c>_pending</c> field in
        /// <see cref="Akka.Streams.Dsl.EagerBatchStage{TIn,TOut}"/>. 🌸
        /// </summary>
        private (bool HasValue, TInput Value) _pending;

        /// <summary>
        /// Creates a new <see cref="ChannelQueueWithBatch{TInput, TBatch}"/> that wraps
        /// the provided <paramref name="inputReader"/> and batches items on read.
        /// </summary>
        /// <param name="inputReader">
        /// The reader side of an input channel owned by the caller. Typically created
        /// from a <c>BoundedChannel&lt;TInput&gt;</c> with <c>FullMode = Wait</c>.
        /// The caller retains the <c>ChannelWriter&lt;TInput&gt;</c> for producing items.
        /// </param>
        /// <param name="maxWeight">
        /// Maximum total cost budget for a single batch. Once accumulated cost reaches
        /// this limit, the batch is emitted and the overflow item is parked for the
        /// next read. Maps to the <c>batch-size</c> config value in
        /// <c>BaseByteArrayJournalDaoConfig</c>. 🌸
        /// </param>
        /// <param name="costFunction">
        /// Computes the cost/weight of a single <typeparamref name="TInput"/> element.
        /// For example, <c>entry =&gt; entry.Rows.Count</c> in the journal DAO.
        /// </param>
        /// <param name="seed">
        /// Creates the initial <typeparamref name="TBatch"/> aggregate from the first
        /// element in a new batch.
        /// </param>
        /// <param name="aggregate">
        /// Folds a subsequent <typeparamref name="TInput"/> element into the running
        /// <typeparamref name="TBatch"/> aggregate.
        /// </param>
        public ChannelQueueWithBatch(
            ChannelReader<TInput> inputReader,
            long maxWeight,
            Func<TInput, long> costFunction,
            Func<TInput, TBatch> seed,
            Func<TBatch, TInput, TBatch> aggregate)
        {
            if (maxWeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxWeight), maxWeight, "Max weight must be greater than zero.");

            _inputReader = inputReader ?? throw new ArgumentNullException(nameof(inputReader));
            _maxWeight = maxWeight;
            _costFunction = costFunction ?? throw new ArgumentNullException(nameof(costFunction));
            _seed = seed ?? throw new ArgumentNullException(nameof(seed));
            _aggregate = aggregate ?? throw new ArgumentNullException(nameof(aggregate));
        }

        /// <summary>
        /// Forwards completion from the underlying input channel reader.
        /// Completes when the input channel is closed (normally or with an error). ✨
        /// </summary>
        public override Task Completion => _inputReader.Completion;

        /// <summary>
        /// Returns <c>true</c> if there is data available to form a batch — either a
        /// pending overflow item from a previous read, or data in the input channel.
        /// Waits asynchronously until at least one item is available or the channel completes.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>
        /// <c>true</c> if data is available for <see cref="TryRead"/>; <c>false</c> if
        /// the input channel has completed and no pending items remain.
        /// </returns>
        public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
        {
            // If we have a pending overflow item, we can form a batch immediately.
            lock (_gate)
            {
                if (_pending.HasValue)
                    return new ValueTask<bool>(true);
            }

            return _inputReader.WaitToReadAsync(cancellationToken);
        }

        /// <summary>
        /// Attempts to read a batch by eagerly draining available items from the input
        /// channel reader, aggregating them via <c>seed</c>/<c>aggregate</c> up to the
        /// <c>maxWeight</c> cost budget.
        ///
        /// <para>
        /// If an item exceeds the remaining budget, it is parked as a pending overflow
        /// item (like <c>EagerBatchStage._pending</c>) and the current batch is returned.
        /// The overflow item becomes the seed of the next batch on the next call. uwu
        /// </para>
        /// </summary>
        /// <param name="item">The aggregated batch, if successful.</param>
        /// <returns><c>true</c> if a batch was produced; <c>false</c> if no items are available.</returns>
        public override bool TryRead(out TBatch item)
        {
            lock (_gate)
            {
                // ── Get the first item: pending overflow or fresh from the input reader ──
                TInput firstItem;
                if (_pending.HasValue)
                {
                    firstItem = _pending.Value;
                    _pending = default;
                }
                else if (!_inputReader.TryRead(out firstItem!))
                {
                    item = default!;
                    return false;
                }

                // ── Seed the batch from the first item ──
                var batch = _seed(firstItem);
                var remaining = _maxWeight - _costFunction(firstItem);

                // ── Eagerly drain: greedily TryRead everything currently available ──
                while (_inputReader.TryRead(out var nextItem))
                {
                    var cost = _costFunction(nextItem);

                    if (cost > remaining)
                    {
                        // This item won't fit in the current batch.
                        // Park it as pending for the next TryRead call.
                        _pending = (true, nextItem);
                        break;
                    }

                    // Room in the batch — fold the element in.
                    batch = _aggregate(batch, nextItem);
                    remaining -= cost;
                }

                item = batch;
                return true;
            }
        }
    }
}
