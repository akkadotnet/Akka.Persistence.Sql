// -----------------------------------------------------------------------
//  <copyright file="EntityActor.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Event;
using Akka.Persistence.Journal;
using Akka.Persistence.Sql.Compat.Common;

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.Internal
{
    public sealed class Truncate : IHasEntityId
    {
        public Truncate(int entityId)
            => EntityId = EntityId = (entityId % Utils.MaxEntities).ToString();

        public string EntityId { get; }
    }

    public sealed class Start : IHasEntityId
    {
        public Start(int entityId)
            => EntityId = EntityId = (entityId % Utils.MaxEntities).ToString();

        public string EntityId { get; }
    }

    public sealed class EntityActor : ReceivePersistentActor
    {
        private readonly ILoggingAdapter _log;

        private bool _clearing;
        private StateSnapshot _lastSnapshot = StateSnapshot.Empty;
        private SnapshotMetadata? _lastSnapshotMetadata;
        private int _persisted;
        private StateSnapshot _savingSnapshot = StateSnapshot.Empty;
        private IActorRef? _sender;
        private int _total;

        public EntityActor(string persistenceId)
        {
            _log = Context.GetLogger();

            PersistenceId = persistenceId;

            Command<int>(
                msg => Persist(
                    msg,
                    i =>
                    {
                        _total += i;
                        _persisted++;
                    }));

            Command<string>(
                msg => Persist(
                    msg,
                    str =>
                    {
                        _total += int.Parse(str);
                        _persisted++;
                    }));

            Command<ShardedMessage>(
                msg =>
                {
                    var obj = msg.ToTagged(msg.Message);
                    switch (obj)
                    {
                        case Tagged tagged:
                            Persist(
                                tagged,
                                sm =>
                                {
                                    _total += ((ShardedMessage)sm.Payload).Message;
                                    _persisted++;
                                });
                            break;

                        default:
                            Persist(
                                msg,
                                sm =>
                                {
                                    _total += sm.Message;
                                    _persisted++;
                                });
                            break;
                    }
                });

            Command<CustomShardedMessage>(
                msg =>
                {
                    var obj = msg.ToTagged(msg.Message);
                    switch (obj)
                    {
                        case Tagged tagged:
                            Persist(
                                tagged,
                                sm =>
                                {
                                    _total += ((CustomShardedMessage)sm.Payload).Message;
                                    _persisted++;
                                });
                            break;

                        default:
                            Persist(
                                msg,
                                sm =>
                                {
                                    _total += sm.Message;
                                    _persisted++;
                                });
                            break;
                    }
                });

            Command<Start>(
                _ => Sender.Tell((PersistenceId, _lastSnapshot, _total, _persisted)));

            Command<Finish>(
                _ => Sender.Tell((PersistenceId, _lastSnapshot, _total, _persisted)));

            Command<Truncate>(
                _ =>
                {
                    _sender = Sender;
                    DeleteMessages(_lastSnapshotMetadata!.SequenceNr);
                });

            Command<TakeSnapshotAndClear>(
                _ =>
                {
                    _sender = Sender;
                    _clearing = true;
                    _savingSnapshot = new StateSnapshot(_total, _persisted);
                    SaveSnapshot(_savingSnapshot);
                });

            Command<TakeSnapshot>(
                _ =>
                {
                    _sender = Sender;
                    _savingSnapshot = new StateSnapshot(_total, _persisted);
                    SaveSnapshot(_savingSnapshot);
                });

            Command<SaveSnapshotSuccess>(
                msg =>
                {
                    _lastSnapshot = _savingSnapshot;
                    _savingSnapshot = StateSnapshot.Empty;

                    if (!_clearing)
                    {
                        _sender.Tell((PersistenceId, _lastSnapshot));
                        return;
                    }

                    _clearing = false;
                    DeleteMessages(msg.Metadata.SequenceNr);
                });

            Command<SaveSnapshotFailure>(
                fail =>
                {
                    _log.Error(fail.Cause, "SaveSnapshot failed!");
                    _savingSnapshot = StateSnapshot.Empty;
                    _sender.Tell((PersistenceId, (StateSnapshot?)null));
                });

            Command<DeleteMessagesSuccess>(
                _ => _sender.Tell((PersistenceId, _lastSnapshot)));

            Command<DeleteMessagesFailure>(
                fail =>
                {
                    _log.Error(fail.Cause, "DeleteMessages failed!");
                    _sender.Tell((PersistenceId, (StateSnapshot?)null));
                });

            Command<RecoveryCompleted>(
                _ =>
                {
                    _log.Info($"{persistenceId}: Recovery completed. State: [Total:{_total}, Persisted:{_persisted}.]");
                    _sender?.Tell(Done.Instance);
                });

            Recover<SnapshotOffer>(
                offer =>
                {
                    _lastSnapshotMetadata = offer.Metadata;
                    _lastSnapshot = (StateSnapshot)offer.Snapshot;
                    _total = _lastSnapshot.Total;
                    _persisted = _lastSnapshot.Persisted;
                    _log.Info(
                        $"{persistenceId}: Snapshot loaded. State: [Total:{_total}, Persisted:{_persisted}.] " +
                        $"Metadata: [SequenceNr:{offer.Metadata.SequenceNr}, Timestamp:{offer.Metadata.Timestamp}]");
                });

            Recover<int>(
                msg =>
                {
                    _total += msg;
                    _persisted++;
                });

            Recover<string>(
                msg =>
                {
                    _total += int.Parse(msg);
                    _persisted++;
                });

            Recover<ShardedMessage>(
                msg =>
                {
                    _total += msg.Message;
                    _persisted++;
                });

            Recover<CustomShardedMessage>(
                msg =>
                {
                    _total += msg.Message;
                    _persisted++;
                });
        }

        public override string PersistenceId { get; }

        public static Props Props(string id)
            => Actor.Props.Create(() => new EntityActor(id));

        protected override void PreStart()
        {
            _log.Debug($"EntityActor({PersistenceId}) started");
            base.PreStart();
        }
    }
}
