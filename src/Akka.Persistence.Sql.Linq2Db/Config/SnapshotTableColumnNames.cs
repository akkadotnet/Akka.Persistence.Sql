using System;
using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db.Config
{
    public class SnapshotTableColumnNames
    {
        public SnapshotTableColumnNames(Configuration.Config config)
        {
            var compat = (config.GetString("table-compatibility-mode", "") ?? "").ToLower();
            var colString = compat switch
            {
                "sqlserver" => "sql-server-compat-column-names",
                "sqlite" => "sqlite-compat-column-names",
                "postgres" => "postgres-compat-column-names",
                "mysql" => "mysql-compat-column-names",
                _ => "column-names"
            };
            
            var cfg = config.GetConfig($"tables.snapshot.{colString}");
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
            return HashCode.Combine(PersistenceId, SequenceNumber, Created, Snapshot, Manifest, SerializerId);
        }
    }
}