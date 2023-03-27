// -----------------------------------------------------------------------
//  <copyright file="LongSnapshotRow.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using LinqToDB;
using LinqToDB.Mapping;

namespace Akka.Persistence.Sql.Snapshot
{
    public class LongSnapshotRow
    {
        [PrimaryKey, NotNull]
        public string PersistenceId { get; set; }

        [PrimaryKey]
        public long SequenceNumber { get; set; }

        [Column(DataType = DataType.Int64)]
        public long Created { get; set; }

        public byte[] Payload { get; set; }

        public string Manifest { get; set; }

        public int? SerializerId { get; set; }
    }
}
