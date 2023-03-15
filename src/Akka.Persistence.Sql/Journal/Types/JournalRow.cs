// -----------------------------------------------------------------------
//  <copyright file="JournalRow.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Journal.Types
{
    public sealed class JournalRow
    {
        public long Ordering { get; set; }

        public long Timestamp { get; set; } = 0;

        public bool Deleted { get; set; }

        public string PersistenceId { get; set; }

        public long SequenceNumber { get; set; }

        public byte[] Message { get; set; }

        public string Tags { get; set; }

        public string Manifest { get; set; }

        public int? Identifier { get; set; }

        public string[] TagArr { get; set; }

        public Guid? WriteUuid { get; set; }

        public string EventManifest { get; set; }
    }
}
