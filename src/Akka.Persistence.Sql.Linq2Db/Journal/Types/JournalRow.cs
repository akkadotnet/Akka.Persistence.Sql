using LinqToDB;
using LinqToDB.Mapping;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Types
{
    public sealed class JournalRow
    {
        public JournalRow()
        {
            
        }
        public long ordering { get; set; }
        
        public long Timestamp { get; set; } = 0;

        public bool deleted { get; set; }
        public string persistenceId { get; set; }
        
        public long sequenceNumber { get; set; }
        
        public byte[] message { get; set; }
        public string tags { get; set; }
        public string manifest { get; set; }
        public int? Identifier { get; set; }
    }
}