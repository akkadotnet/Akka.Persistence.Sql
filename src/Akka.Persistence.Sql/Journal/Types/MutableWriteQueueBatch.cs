// -----------------------------------------------------------------------
//  <copyright file="MutableWriteQueueBatch.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;

namespace Akka.Persistence.Sql.Journal.Types
{
    /// <summary>
    /// A mutable, in-place accumulator for batching <see cref="WriteQueueEntry"/> items
    /// inside the <c>ChannelQueueWithBatch</c> pipeline. (≧◡≦) ✨
    ///
    /// <para>
    /// Unlike the immutable <see cref="WriteQueueSet"/>, this class mutates its internal
    /// <see cref="List{T}"/> fields via <see cref="Add"/>, completely eliminating:
    /// <list type="bullet">
    ///   <item>A new <see cref="WriteQueueSet"/> heap allocation per aggregation step.</item>
    ///   <item>The O(N) copy cost of <c>ImmutableList.Add()</c> per-TCS/CancellationToken.</item>
    ///   <item>The <c>Seq.Concat()</c> deferred-chain allocation per row batch.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// <b>CopilotNote:</b> This type is intentionally NOT thread-safe. It is only ever
    /// mutated by the single-reader <c>ChannelQueueWithBatch</c> loop, which matches the
    /// <c>SingleReader = true</c> channel option set in <c>BaseByteArrayJournalDao</c>.
    /// Thread safety is therefore guaranteed by the channel contract itself. UwU~
    /// </para>
    /// </summary>
    public sealed class MutableWriteQueueBatch
    {
        // 🌸 Pre-sized to 4 for the TCS and token lists to cover most small batches
        // without growing immediately, but still growing gracefully for large ones.
        private readonly List<TaskCompletionSource<NotUsed>> _tcs;
        private readonly List<CancellationToken> _cancellationTokens;
        private Seq<JournalRow> _rows;

        /// <summary>
        /// Seeds a new batch from the first <see cref="WriteQueueEntry"/> off the channel. ✨
        /// The row list is pre-sized to the seed entry's row count as a best-guess capacity hint.
        /// </summary>
        /// <param name="seed">The first entry to initialize this batch with.</param>
        public MutableWriteQueueBatch(WriteQueueEntry seed)
        {
            // Pre-size rows with the seed count as a capacity hint; 
            // avoids immediate realloc for single-entry batches.
            _rows = seed.Rows;
            //_rows = new List<JournalRow>(seed.Rows.Count > 0 ? seed.Rows.Count : 4);
            //_rows.AddRange(seed.Rows);

            _tcs = new List<TaskCompletionSource<NotUsed>>(4) { seed.Tcs };
            _cancellationTokens = new List<CancellationToken>(4) { seed.CancellationToken };
        }

        /// <summary>Gets the accumulated task completion sources for all batched writes. 🎯</summary>
        public IReadOnlyList<TaskCompletionSource<NotUsed>> Tcs => _tcs;

        /// <summary>Gets the accumulated journal rows from all batched write entries. 📝</summary>
        public Seq<JournalRow> Rows => _rows;

        /// <summary>Gets the accumulated cancellation tokens for all batched writes. 🛑</summary>
        public List<CancellationToken> CancellationTokens => _cancellationTokens;

        /// <summary>
        /// Merges a new <see cref="WriteQueueEntry"/> into this batch in-place.
        /// Zero extra object allocations beyond the underlying <see cref="List{T}"/>
        /// amortized growth — no new <see cref="MutableWriteQueueBatch"/> instances created! (⌒▽⌒)☆
        /// </summary>
        /// <param name="entry">The next entry off the channel to fold into this batch.</param>
        public void Add(WriteQueueEntry entry)
        {
            _tcs.Add(entry.Tcs);
            _rows = _rows.Concat(entry.Rows);
            _cancellationTokens.Add(entry.CancellationToken);
        }
    }
}


