// -----------------------------------------------------------------------
//  <copyright file="SnapshotTableColumnNames.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Config
{
    public class SnapshotTableColumnNames: IEquatable<SnapshotTableColumnNames>
    {
        public SnapshotTableColumnNames(Configuration.Config config)
        {
            var cfg = config.GetConfig("columns");
            PersistenceId = cfg.GetString("persistence-id", "persistence_id");
            SequenceNumber = cfg.GetString("sequence-number", "sequence_number");
            Created = cfg.GetString("created", "created");
            Snapshot = cfg.GetString("snapshot", "snapshot");
            Manifest = cfg.GetString("manifest", "manifest");
            SerializerId = cfg.GetString("serializerId", "serializer_id");
        }

        public string PersistenceId { get; }

        public string SequenceNumber { get; }

        public string Created { get; }

        public string Snapshot { get; }

        public string Manifest { get; }

        public string SerializerId { get; }

        public override int GetHashCode()
            => HashCode.Combine(PersistenceId, SequenceNumber, Created, Snapshot, Manifest, SerializerId);

        public bool Equals(SnapshotTableColumnNames? other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return PersistenceId == other.PersistenceId && 
                   SequenceNumber == other.SequenceNumber && 
                   Created == other.Created && 
                   Snapshot == other.Snapshot && 
                   Manifest == other.Manifest && 
                   SerializerId == other.SerializerId;
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            return obj is SnapshotTableColumnNames other && Equals(other);
        }
    }
}
