// -----------------------------------------------------------------------
//  <copyright file="MetadataTableColumnNames.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Config
{
    public class MetadataTableColumnNames : IEquatable<MetadataTableColumnNames>
    {
        public MetadataTableColumnNames(Configuration.Config config)
        {
            var cfg = config.GetConfig("columns");
            PersistenceId = cfg.GetString("persistence-id", "persistence_id");
            SequenceNumber = cfg.GetString("sequence-number", "sequence_number");
        }

        public string PersistenceId { get; }

        public string SequenceNumber { get; }

        public bool Equals(MetadataTableColumnNames other)
        {
            if (ReferenceEquals(null, other))
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return PersistenceId == other.PersistenceId && SequenceNumber == other.SequenceNumber;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            return obj is MetadataTableColumnNames m && Equals(m);
        }

        public override int GetHashCode()
            => HashCode.Combine(PersistenceId, SequenceNumber);
    }
}
