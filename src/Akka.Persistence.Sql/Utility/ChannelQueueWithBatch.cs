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
    /// drains as many items as are currently available from the input reader (up to the weight
    /// budget), aggregates them via the <c>seed</c>/<c>aggregate</c> delegates, and returns the batch.
    ///
    /// <para>
    /// This mirrors the semantics of Akka.Streams <c>BatchWeighted</c>, but runs on
    /// <c>System.Threading.Channels</c>, so it can be consumed with
    /// <c>Source.ChannelReader(batcher)</c>, <c>ReadAllAsync</c> or <c>ReadAsync</c>.
    /// </para>
    ///
    /// <para>
    /// The caller creates and owns the input channel and keeps its writer. This class only
    /// covers the batching step; it is intended for a single reader.
    /// </para>
    /// </summary>
    /// <typeparam name="TInput">The type of individual items read from the input channel.</typeparam>
    /// <typeparam name="TBatch">The aggregated batch type emitted to consumers.</typeparam>
    internal sealed class ChannelQueueWithBatch<TInput, TBatch> : ChannelReader<TBatch>
    {
        private readonly ChannelReader<TInput> _inputReader;
        private readonly long _maxWeight;
        private readonly Func<TInput, long> _costFunction;
        private readonly Func<TInput, TBatch> _seed;
        private readonly Func<TBatch, TInput, TBatch> _aggregate;

        /// <summary>
        /// Guards <see cref="_pending"/>. The critical sections are fully synchronous.
        /// </summary>
        private readonly object _gate = new();

        /// <summary>
        /// Item that did not fit in the previous batch; it seeds the next one.
        /// </summary>
        private (bool HasValue, TInput Value) _pending;

        /// <param name="inputReader">
        /// The reader side of an input channel owned by the caller, typically a bounded channel.
        /// </param>
        /// <param name="maxWeight">
        /// Maximum total cost of a single batch. An item that would exceed the remaining budget is
        /// held back and seeds the next batch. A single item heavier than the budget still forms
        /// a batch on its own.
        /// </param>
        /// <param name="costFunction">Computes the cost of a single item.</param>
        /// <param name="seed">Creates a new batch from its first item.</param>
        /// <param name="aggregate">Folds a further item into the batch.</param>
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
        /// Forwards completion from the input channel reader.
        /// </summary>
        public override Task Completion => _inputReader.Completion;

        /// <summary>
        /// Returns <c>true</c> if a batch can be formed, either from an overflow item held back by the
        /// previous read or from data in the input channel. Returns <c>false</c> once the input channel
        /// has completed and nothing is left.
        /// </summary>
        public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_pending.HasValue)
                    return new ValueTask<bool>(true);
            }

            return _inputReader.WaitToReadAsync(cancellationToken);
        }

        /// <summary>
        /// Reads a batch by draining the items currently available in the input channel, up to the
        /// weight budget. An item that does not fit is held back and seeds the next batch.
        /// </summary>
        /// <param name="item">The aggregated batch, if successful.</param>
        /// <returns><c>true</c> if a batch was produced; <c>false</c> if no items are available.</returns>
        public override bool TryRead(out TBatch item)
        {
            lock (_gate)
            {
                return TryReadBatch(out item);
            }
        }

        private bool TryReadBatch(out TBatch item)
        {
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

            var batch = _seed(firstItem);
            var remaining = _maxWeight - _costFunction(firstItem);

            while (_inputReader.TryRead(out var nextItem))
            {
                var cost = _costFunction(nextItem);
                if (cost > remaining)
                {
                    _pending = (true, nextItem);
                    break;
                }

                batch = _aggregate(batch, nextItem);
                remaining -= cost;
            }

            item = batch;
            return true;
        }
    }
}
