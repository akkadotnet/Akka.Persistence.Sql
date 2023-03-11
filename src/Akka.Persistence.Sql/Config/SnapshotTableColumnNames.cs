using System;
using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db.Config
{
    public class SnapshotTableColumnNames
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
        {
            return HashCode.Combine(PersistenceId, SequenceNumber, Created, Snapshot, Manifest, SerializerId);
        }
    }
}