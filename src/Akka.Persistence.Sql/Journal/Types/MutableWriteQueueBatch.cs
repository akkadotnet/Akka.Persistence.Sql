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
    /// Batch of <see cref="WriteQueueEntry"/> items used by the <c>channel</c> write queue mode.
    ///
    /// <para>
    /// Unlike the immutable <see cref="WriteQueueSet"/>, entries are added in place, so aggregating
    /// an entry does not allocate a new batch object or copy the promise and token lists.
    /// </para>
    ///
    /// <para>
    /// Not thread-safe. A batch is only built by the single reader of the write channel and is
    /// handed downstream only after it is complete.
    /// </para>
    /// </summary>
    internal sealed class MutableWriteQueueBatch
    {
        private readonly List<TaskCompletionSource<NotUsed>> _tcs;
        private readonly List<CancellationToken> _cancellationTokens;
        private Seq<JournalRow> _rows;

        public MutableWriteQueueBatch(WriteQueueEntry seed)
        {
            _rows = seed.Rows;
            _tcs = new List<TaskCompletionSource<NotUsed>>(4) { seed.Tcs };
            _cancellationTokens = new List<CancellationToken>(4) { seed.CancellationToken };
        }

        public IReadOnlyList<TaskCompletionSource<NotUsed>> Tcs => _tcs;

        public Seq<JournalRow> Rows => _rows;

        public IReadOnlyList<CancellationToken> CancellationTokens => _cancellationTokens;

        public void Add(WriteQueueEntry entry)
        {
            _tcs.Add(entry.Tcs);
            _rows = _rows.Concat(entry.Rows);
            _cancellationTokens.Add(entry.CancellationToken);
        }
    }
}
