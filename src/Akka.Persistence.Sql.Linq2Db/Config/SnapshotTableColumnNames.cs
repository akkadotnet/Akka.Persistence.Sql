using System;
using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db.Config
{
    public class SnapshotTableColumnNames
    {
        public SnapshotTableColumnNames(Configuration.Config config)
        {
            var compat = (config.GetString("table-compatibility-mode", "")??"").ToLower();
            string colString;
            switch (compat)
            {
                case "sqlserver":
                    colString = "sql-server-compat-column-names";
                    break;
                case "sqlite":
                    colString = "sqlite-compat-column-names";
                    break;
                case "postgres":
                    colString = "postgres-compat-column-names";
                    break;
                case "mysql":
                    colString = "mysql-compat-column-names";
                    break;
                default:
                    colString = "column-names";
                    break;
            }
            var cfg = config
                .GetConfig($"tables.snapshot.{colString}").SafeWithFallback(
                    ConfigurationFactory.ParseString(FallBack).GetConfig($"tables.snapshot.{colString}"));

            PersistenceId = cfg.GetString("persistenceId", "persistence_id");
            SequenceNumber = cfg.GetString("sequenceNumber", "sequence_number");
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
            return HashCode.Combine(PersistenceId, SequenceNumber, Created,
                Snapshot, Manifest, SerializerId);
        }

        private static readonly string FallBack = @"
column-names {
    persistenceId = persistence_id
    sequenceNumber = sequence_number
    created = created
    snapshot = snapshot
    manifest = manifest
    serializerId = serializer_id
}
sql-server-compat-column-names {
    persistenceId = PersistenceId
    sequenceNumber = SequenceNr
    created = Timestamp
    snapshot = Snapshot
    manifest = Manifest
    serializerId = SerializerId
}
sqlite-compat-column-names {
    persistenceId = persistence_id
    sequenceNumber = sequence_nr
    created = created_at
    snapshot = payload
    manifest = manifest
    serializerId = serializer_id
}
postgres-compat-column-names {
    persistenceId: persistence_id,
    sequenceNumber: sequence_nr,
    created: created_at,
    snapshot: payload,
    manifest: manifest,
    serializerId: serializer_id,
}
mysql-compat-column-names {
    persistenceId: persistence_id,
    sequenceNumber: sequence_nr,
    created: created_at,
    snapshot: snapshot,
    manifest: manifest,
    serializerId: serializer_id,
}
";
    }
}