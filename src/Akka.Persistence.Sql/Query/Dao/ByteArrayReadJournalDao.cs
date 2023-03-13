using Akka.Actor;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Serialization;
using Akka.Streams;

namespace Akka.Persistence.Sql.Query.Dao
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
