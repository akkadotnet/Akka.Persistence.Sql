using System;
using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db.Config
{
    public class MetadataTableColumnNames
    {
        public string PersistenceId { get; }
        
        public string SequenceNumber { get; }
        
        public MetadataTableColumnNames(Configuration.Config config)
        {
            var compat = (config.GetString("table-compatibility-mode", "") ?? "").ToLower();
            var colString = compat switch
            {
                "sqlserver" => "sqlserver-compat-metadata-column-names",
                "sqlite" => "sqlite-compat-metadata-column-names",
                "postgres" => "postgres-compat-metadata-column-names",
                "mysql" => "mysql-compat-metadata-column-names",
                _ => "metadata-column-names"
            };
            
            var cfg = config.GetConfig($"tables.journal.{colString}");
            PersistenceId =  cfg.GetString("persistenceId", "PersistenceId");
            SequenceNumber = cfg.GetString("sequenceNumber", "sequenceNr");
        }
        protected bool Equals(MetadataTableColumnNames other)
        {
            return PersistenceId == other.PersistenceId && SequenceNumber == other.SequenceNumber;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is MetadataTableColumnNames m && Equals(m);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PersistenceId, SequenceNumber);
        }
    }
}