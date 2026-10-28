// -----------------------------------------------------------------------
//  <copyright file="WriteQueueSet.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;

namespace Akka.Persistence.Sql.Journal.Types
{
    public sealed class WriteQueueSet
    {
        public WriteQueueSet(ImmutableList<TaskCompletionSource<NotUsed>> tcs, Seq<JournalRow> rows, ImmutableList<CancellationToken> cancellationTokens)
        {
            Tcs = tcs;
            Rows = rows;
            CancellationTokens = cancellationTokens;
        }

        public Seq<JournalRow> Rows { get; }

        public ImmutableList<TaskCompletionSource<NotUsed>> Tcs { get; }
        
        public ImmutableList<CancellationToken> CancellationTokens { get; }
        
    }
}
