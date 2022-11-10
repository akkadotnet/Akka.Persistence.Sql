using Akka.Actor;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Db;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using Akka.Persistence.Sql.Linq2Db.Serialization;
using Akka.Streams;

namespace Akka.Persistence.Sql.Linq2Db.Query.Dao
{
    public class ByteArrayReadJournalDao : BaseByteReadArrayJournalDao
    {
        public ByteArrayReadJournalDao(
            IAdvancedScheduler scheduler,
            IMaterializer materializer,
            AkkaPersistenceDataConnectionFactory connectionFactory,
            ReadJournalConfig readJournalConfig,
            FlowPersistentReprSerializer<JournalRow> serializer) 
            : base(scheduler, materializer, connectionFactory, readJournalConfig, serializer)
        {
        }
    }
}