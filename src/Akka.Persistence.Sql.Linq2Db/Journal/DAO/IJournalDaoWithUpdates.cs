using System.Threading.Tasks;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Dao
{
    public interface IJournalDaoWithUpdates : IJournalDao
    {
        Task<Done> Update(string persistenceId, long sequenceNr, object payload);
    }
}