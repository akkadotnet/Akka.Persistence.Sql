using System;
using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db.Config
{
    public class MetadataTableColumnNames: IEquatable<MetadataTableColumnNames>
    {
        public string PersistenceId { get; }
        
        public string SequenceNumber { get; }
        
        public MetadataTableColumnNames(Configuration.Config config)
        {
            var cfg = config.GetConfig("columns");
            PersistenceId =  cfg.GetString("persistenceId", "PersistenceId");
            SequenceNumber = cfg.GetString("sequenceNumber", "sequenceNr");
        }
        public bool Equals(MetadataTableColumnNames other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
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