using System;
using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db.Config
{
    public class JournalTableColumnNames
    {
        public JournalTableColumnNames(Configuration.Config config)
        {
            var cfg = config.GetConfig("columns");
            Ordering = cfg.GetString("ordering","ordering");
            Deleted = cfg.GetString("deleted","deleted");
            PersistenceId = cfg.GetString("persistenceId", "persistence_id");
            SequenceNumber = cfg.GetString("sequenceNumber", "sequence_number");
            Created = cfg.GetString("created", "created");
            Tags = cfg.GetString("tags", "tags");
            Message = cfg.GetString("message", "message");
            Identifier = cfg.GetString("identifier", "identifier");
            Manifest = cfg.GetString("manifest", "manifest");
        }
        public string Ordering { get; }
        public string Deleted { get; }
        public string PersistenceId { get; }
        public string SequenceNumber { get; }
        public string Created { get; }
        public string Tags { get; }
        public string Message { get; }
        public string Identifier { get; }
        public string Manifest { get; }
        
        private bool Equals(JournalTableColumnNames other)
        {
            return 
                Ordering == other.Ordering && 
                Deleted == other.Deleted &&
                PersistenceId == other.PersistenceId &&
                SequenceNumber == other.SequenceNumber &&
                Created == other.Created && 
                Tags == other.Tags &&
                Message == other.Message &&
                Identifier == other.Identifier &&
                Manifest == other.Manifest;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is JournalTableColumnNames j && Equals(j);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(Ordering);
            hashCode.Add(Deleted);
            hashCode.Add(PersistenceId);
            hashCode.Add(SequenceNumber);
            hashCode.Add(Created);
            hashCode.Add(Tags);
            hashCode.Add(Message);
            hashCode.Add(Identifier);
            hashCode.Add(Manifest);
            return hashCode.ToHashCode();
        }
    }
}