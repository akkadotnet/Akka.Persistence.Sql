// -----------------------------------------------------------------------
//  <copyright file="SnapshotCompatibilityActor.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Persistence.Sql.Tests.Internal.Events;

namespace Akka.Persistence.Sql.Tests.Internal
{
    public class SnapshotCompatibilityActor : ReceivePersistentActor
    {
        private IActorRef _sender;

        private List<SomeEvent> _events = new();

        public SnapshotCompatibilityActor(string snapshot, string persistenceId)
        {
            JournalPluginId = "akka.persistence.journal.inmem";
            SnapshotPluginId = snapshot;
            PersistenceId = persistenceId;

            Command<SomeEvent>(
                someEvent =>
                {
                    _sender = Sender;
                    Persist(
                        someEvent,
                        p =>
                        {
                            _events.Add(p);
                            SaveSnapshot(_events);
                        });
                });

            Command<ContainsEvent>(
                containsEvent => Context.Sender.Tell(_events.Any(e => e.Guid == containsEvent.Guid)));

            Command<SaveSnapshotSuccess>(_ => _sender.Tell(true));
            Command<SaveSnapshotFailure>(_ => _sender.Tell(false));

            Recover<SnapshotOffer>(
                snapshotOffer =>
                {
                    if (snapshotOffer.Snapshot is List<SomeEvent> sel)
                        _events = sel;
                });
        }

        public override string PersistenceId { get; }
    }
}
