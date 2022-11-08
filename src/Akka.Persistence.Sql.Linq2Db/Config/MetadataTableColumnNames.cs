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
            var compat = (config.GetString("table-compatibility-mode", "")??"").ToLower();
            string colString;
            switch (compat)
            {
                case "sqlserver":
                    colString = "sqlserver-compat-metadata-column-names";
                    break;
                case "sqlite":
                    colString = "sqlite-compat-metadata-column-names";
                    break;
                case "postgres":
                    colString = "postgres-compat-metadata-column-names";
                    break;
                case "mysql":
                    colString = "mysql-compat-metadata-column-names";
                    break;
                default:
                    colString = "metadata-column-names";
                    break;
            }
            var cfg = config
                .GetConfig($"tables.journal.{colString}");
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
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MetadataTableColumnNames) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PersistenceId, SequenceNumber);
        }
    }
}