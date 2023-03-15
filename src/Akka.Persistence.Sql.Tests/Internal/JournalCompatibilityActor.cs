// -----------------------------------------------------------------------
//  <copyright file="JournalCompatibilityActor.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Persistence.Sql.Tests.Internal.Events;

namespace Akka.Persistence.Sql.Tests.Internal
{
    public class GetSequenceNr { }

    public class CurrentSequenceNr
    {
        public CurrentSequenceNr(long sn)
            => SequenceNumber = sn;

        public long SequenceNumber { get; }
    }

    public class DeleteUpToSequenceNumber
    {
        public DeleteUpToSequenceNumber(long nr)
            => Number = nr;

        public long Number { get; }
    }

    public class JournalCompatibilityActor : ReceivePersistentActor
    {
        private readonly List<SomeEvent> _events = new();

        private IActorRef _deleteSubscriber;

        public JournalCompatibilityActor(string journal, string persistenceId)
        {
            JournalPluginId = journal;
            PersistenceId = persistenceId;

            Command<SomeEvent>(
                someEvent =>
                {
                    var sender = Sender;
                    Persist(
                        someEvent,
                        p =>
                        {
                            _events.Add(p);
                            sender.Tell(someEvent);
                        });
                });

            Command<ContainsEvent>(
                containsEvent => Context.Sender.Tell(_events.Any(e => e.Guid == containsEvent.Guid)));

            Command<GetSequenceNr>(
                _ => Context.Sender.Tell(new CurrentSequenceNr(LastSequenceNr)));

            Command<DeleteUpToSequenceNumber>(
                deleteUpToSequenceNumber =>
                {
                    _deleteSubscriber = Context.Sender;
                    DeleteMessages(deleteUpToSequenceNumber.Number);
                });

            Command<DeleteMessagesSuccess>(
                deleteMessagesSuccess => _deleteSubscriber?.Tell(deleteMessagesSuccess));

            Command<DeleteMessagesFailure>(
                deleteMessagesFailure => _deleteSubscriber?.Tell(deleteMessagesFailure));

            Recover<SomeEvent>(_events.Add);
        }

        public override string PersistenceId { get; }
    }
}
