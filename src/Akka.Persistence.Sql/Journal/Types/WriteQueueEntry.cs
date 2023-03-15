// -----------------------------------------------------------------------
//  <copyright file="WriteQueueEntry.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using LanguageExt;

namespace Akka.Persistence.Sql.Journal.Types
{
    public sealed class WriteQueueEntry
    {
        public WriteQueueEntry(TaskCompletionSource<NotUsed> tcs, Seq<JournalRow> rows)
        {
            Tcs = tcs;
            Rows = rows;
        }

        public Seq<JournalRow> Rows { get; }

        public TaskCompletionSource<NotUsed> Tcs { get; }
    }
}
