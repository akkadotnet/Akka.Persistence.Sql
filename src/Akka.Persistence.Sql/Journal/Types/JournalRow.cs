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

        public long Timestamp { get; set; }

        public bool Deleted { get; set; }

        public string PersistenceId { get; set; }  = string.Empty;

        public long SequenceNumber { get; set; }

        public byte[] Message { get; set; } = Array.Empty<byte>();

        public string? Tags { get; set; } = string.Empty;

        public string Manifest { get; set; }  = string.Empty;

        public int? Identifier { get; set; }

        // ReSharper disable once InconsistentNaming
        public string[] TagArray { get; set; } = Array.Empty<string>();

        public string? WriterUuid { get; set; }

        public string? EventManifest { get; set; }
    }
}
