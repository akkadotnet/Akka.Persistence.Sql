using System.Threading.Tasks;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Types
{
    public sealed class WriteFinished
    {
        public WriteFinished(string persistenceId, Task future)
        {
            PersistenceId = persistenceId;
            Future = future;
        }
        
        public string PersistenceId { get; }
        
        public Task Future { get; }
    }
}