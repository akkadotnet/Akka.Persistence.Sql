// -----------------------------------------------------------------------
//  <copyright file="JournalTagRow.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Journal.Types
{
    public sealed class JournalTagRow
    {
        public long SequenceNumber { get; set; }

        public string PersistenceId { get; set; }

        public long OrderingId { get; set; }

        public string TagValue { get; set; }
    }
}
