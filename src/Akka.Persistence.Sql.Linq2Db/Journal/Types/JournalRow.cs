using System;
using LinqToDB;
using LinqToDB.Mapping;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Types
{
    public sealed class JournalTagRow
    {
        public long JournalOrderingId { get; set; }
        
        public string TagValue { get; set; }
        
        public Guid WriteUUID { get; set; }
    }
    
    public sealed class JournalRow
    {
        public long Ordering { get; set; }
        
        public long Timestamp { get; set; } = 0;

        public bool Deleted { get; set; }
        
        public string PersistenceId { get; set; }
        
        public long SequenceNumber { get; set; }
        
        public byte[] Message { get; set; }
        
        public string Tags { get; set; }
        
        public string Manifest { get; set; }
        
        public int? Identifier { get; set; }
        
        public string[] TagArr { get; set; }
        
        public Guid? WriteUUID { get; set; }
        
        public string eventManifest { get; set; }
    }
}