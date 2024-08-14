// -----------------------------------------------------------------------
//  <copyright file="DateTimeSnapshotRow.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using LinqToDB;
using LinqToDB.Mapping;

namespace Akka.Persistence.Sql.Snapshot
{
    public class DateTimeSnapshotRow
    {
        [PrimaryKey]
        [NotNull]
        public string PersistenceId { get; set; } = string.Empty;

        [PrimaryKey]
        public long SequenceNumber { get; set; }

        [Column(DataType = DataType.DateTime2)]
        public DateTime Created { get; set; }

        public byte[] Payload { get; set; } = Array.Empty<byte>();

        public string Manifest { get; set; } = string.Empty;

        public int? SerializerId { get; set; }
    }
}
