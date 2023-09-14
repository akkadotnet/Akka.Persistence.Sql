// -----------------------------------------------------------------------
//  <copyright file="PersistenceActor.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Event;
using Akka.Persistence;

namespace HoconConfiguration
{
    public class TestPersistenceActor: ReceivePersistentActor
    {
        public static Props Props(string id) => Akka.Actor.Props.Create(() => new TestPersistenceActor(id));
        
        public override string PersistenceId { get; }

        private int _counter;
        private string _state = string.Empty;

        public TestPersistenceActor(string persistenceId)
        {
            PersistenceId = persistenceId;
            var log = Context.GetLogger();
            
            Recover<SnapshotOffer>(s => _state = (string)s.Snapshot);
            Recover<string>(s => _state += s);
            
            Command<string>(
                msg =>
                {
                    Persist(msg,
                        s =>
                        {
                            _state += s;
                            _counter++;
                            if(_counter % 25 == 0)
                                SaveSnapshot(_state);
                            log.Info($"Persisted message: {s}");
                        });
                });
            Command<SaveSnapshotSuccess>(
                _ =>
                {
                    log.Info($"Snapshot persisted. State: {_state}");
                });
        }
    }
}
