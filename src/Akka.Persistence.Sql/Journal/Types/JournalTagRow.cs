// -----------------------------------------------------------------------
//  <copyright file="JournalTagRow.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Journal.Types
{
    // ReSharper disable once InconsistentNaming
    public class TagRow
    {
        public long OrderingId { get; set; }

        // ReSharper disable once InconsistentNaming
        public string TagValue { get; set; }
    }

    public sealed class JournalTagRow : TagRow
    {
        public long SequenceNumber { get; set; }

        public string PersistenceId { get; set; }
    }
}
