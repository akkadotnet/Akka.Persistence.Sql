// -----------------------------------------------------------------------
//  <copyright file="JournalMetaData.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using LinqToDB.Mapping;

namespace Akka.Persistence.Sql.Journal.Types
{
    public sealed class JournalMetaData
    {
        [Column(IsPrimaryKey = true, CanBeNull = false)]
        public string PersistenceId { get; set; }

        [PrimaryKey]
        public long SequenceNumber { get; set; }
    }
}
