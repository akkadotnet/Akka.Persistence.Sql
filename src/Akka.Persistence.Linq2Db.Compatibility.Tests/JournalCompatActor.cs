using System.Collections.Generic;
using System.Linq;
using Akka.Actor;

namespace Akka.Persistence.Linq2Db.CompatibilityTests
{
    public class GetSequenceNr
    {
        
    }

    public class DeleteUpToSequenceNumber
    {
        public DeleteUpToSequenceNumber(long nr)
        {
            Number = nr;
        }

        public long Number { get; }
    }
    public class JournalCompatActor : ReceivePersistentActor
    {
        private readonly List<SomeEvent> _events = new List<SomeEvent>();
        private IActorRef _deleteSubscriber;
        public JournalCompatActor(string journal, string persistenceId)
        {
            JournalPluginId = journal;
            PersistenceId = persistenceId;
            Command<SomeEvent>(se =>
            {
                var sender = Sender;
                Persist(se, p =>
                {
                    _events.Add(p);
                    sender.Tell(se);
                });
            });
            Command<ContainsEvent>(ce=>Context.Sender.Tell(_events.Any(e=>e.Guid==ce.Guid)));
            Command<GetSequenceNr>(gsn =>
                Context.Sender.Tell(
                    new CurrentSequenceNr(LastSequenceNr)));
            Command<DeleteUpToSequenceNumber>(dc =>
            {
                _deleteSubscriber = Context.Sender;
                DeleteMessages(dc.Number);
            });
            Command<DeleteMessagesSuccess>(dms => _deleteSubscriber?.Tell(dms));
            Command<DeleteMessagesFailure>(dmf=>_deleteSubscriber?.Tell(dmf));
            Recover<SomeEvent>(se => _events.Add(se));
        }
        public override string PersistenceId { get; }
    }

    public class CurrentSequenceNr
    {
        public CurrentSequenceNr(long sn)
        {
            SequenceNumber = sn;
        }

        public long SequenceNumber { get; }
    }
}