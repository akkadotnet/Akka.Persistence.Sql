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
        private List<SomeEvent> events = new List<SomeEvent>();
        private IActorRef deleteSubscriber;
        public JournalCompatActor(string journal, string persistenceId)
        {
            JournalPluginId = journal;
            PersistenceId = persistenceId;
            Command<SomeEvent>(se =>
            {
                var sender = Sender;
                Persist(se, p =>
                {
                    events.Add(p);
                    sender.Tell(se);
                });
            });
            Command<ContainsEvent>(ce=>Context.Sender.Tell(events.Any(e=>e.Guid==ce.Guid)));
            Command<GetSequenceNr>(gsn =>
                Context.Sender.Tell(
                    new CurrentSequenceNr(this.LastSequenceNr)));
            Command<DeleteUpToSequenceNumber>(dc =>
            {
                deleteSubscriber = Context.Sender;
                DeleteMessages(dc.Number);
            });
            Command<DeleteMessagesSuccess>(dms => deleteSubscriber?.Tell(dms));
            Command<DeleteMessagesFailure>(dmf=>deleteSubscriber?.Tell(dmf));
            Recover<SomeEvent>(se => events.Add(se));
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