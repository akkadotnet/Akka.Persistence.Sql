// -----------------------------------------------------------------------
//  <copyright file="SqlReadJournal.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Pattern;
using Akka.Persistence.Journal;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Journal.Dao;
using Akka.Persistence.Sql.Query.Dao;
using Akka.Persistence.Sql.Query.InternalProtocol;
using Akka.Persistence.Sql.Utility;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;

namespace Akka.Persistence.Sql.Query
{
    public class SqlReadJournal :
        IPersistenceIdsQuery,
        ICurrentPersistenceIdsQuery,
        IEventsByPersistenceIdQuery,
        ICurrentEventsByPersistenceIdQuery,
        IEventsByTagQuery,
        ICurrentEventsByTagQuery,
        IAllEventsQuery,
        ICurrentAllEventsQuery
    {
        // ReSharper disable once UnusedMember.Global
        [Obsolete(message: "Use SqlPersistence.Get(ActorSystem).DefaultConfig instead")]
        public static readonly Configuration.Config DefaultConfiguration = SqlWriteJournal.DefaultConfiguration;

        private readonly Source<long, ICancelable> _delaySource;
        private readonly EventAdapters _eventAdapters;
        private readonly IActorRef _journalSequenceActor;
        private readonly ActorMaterializer _mat;
        private readonly ReadJournalConfig _readJournalConfig;
        private readonly ByteArrayReadJournalDao _readJournalDao;
        private readonly ExtendedActorSystem _system;

        public SqlReadJournal(
            ExtendedActorSystem system,
            Configuration.Config config)
        {
            var writePluginId = config.GetString("write-plugin");
            _eventAdapters = Persistence.Instance.Apply(system).AdaptersFor(writePluginId);

            _readJournalConfig = new ReadJournalConfig(config);
            _system = system;

            var connFact = new AkkaPersistenceDataConnectionFactory(_readJournalConfig);
            
            // Fix for https://github.com/akkadotnet/Akka.Persistence.Sql/issues/344
            var writeJournal = Persistence.Instance.Apply(system).JournalFor(writePluginId);
            // we want to block, we want to crash if the journal is not available
            var started = writeJournal.Ask<Initialized>(IsInitialized.Instance, TimeSpan.FromSeconds(5)).Result;

            _mat = Materializer.CreateSystemMaterializer(
                context: system,
                settings: ActorMaterializerSettings.Create(system),
                namePrefix: $"l2db-query-mat-{Guid.NewGuid():N}");

            _readJournalDao = new ByteArrayReadJournalDao(
                scheduler: system.Scheduler.Advanced,
                materializer: _mat,
                connectionFactory: connFact,
                readJournalConfig: _readJournalConfig,
                serializer: new ByteArrayJournalSerializer(
                    journalConfig: _readJournalConfig,
                    serializer: system.Serialization,
                    separator: _readJournalConfig.PluginConfig.TagSeparator,
                    writerUuid: null),
                // TODO: figure out a way to signal shutdown to the query executor here
                default);

            _journalSequenceActor = system.ActorOf(
                props: Props.Create(
                    () => new JournalSequenceActor(
                        _readJournalDao,
                        _readJournalConfig.JournalSequenceRetrievalConfiguration)),
                name: $"{_readJournalConfig.TableConfig.EventJournalTable.Name}akka-persistence-sql-sequence-actor");

            _delaySource = Source.Tick(TimeSpan.FromSeconds(0), _readJournalConfig.RefreshInterval, 0L).Take(1);
        }

        public static string Identifier => "akka.persistence.query.journal.sql";

        public Source<EventEnvelope, NotUsed> AllEvents(Offset offset)
            => Events(
                offset is Sequence s
                    ? s.Value
                    : 0,
                null);

        public Source<EventEnvelope, NotUsed> CurrentAllEvents(Offset offset)
            => AsyncSource<long>
                .FromEnumerable(
                    state: _readJournalDao,
                    func: async input => new[] { await input.MaxJournalSequenceAsync() })
                .ConcatMany(
                    maxInDb =>
                        Events(
                            offset is Sequence s
                                ? s.Value
                                : 0,
                            maxInDb));

        public Source<EventEnvelope, NotUsed> CurrentEventsByPersistenceId(
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr)
            => EventsByPersistenceIdSource(
                persistenceId: persistenceId,
                fromSequenceNr: fromSequenceNr,
                toSequenceNr: toSequenceNr,
                refreshInterval: Option<(TimeSpan, IScheduler)>.None);

        public Source<EventEnvelope, NotUsed> CurrentEventsByTag(string tag, Offset offset)
            => CurrentEventsByTag(tag, (offset as Sequence)?.Value ?? 0);

        public Source<string, NotUsed> CurrentPersistenceIds()
            => _readJournalDao.AllPersistenceIdsSource(long.MaxValue);

        public Source<EventEnvelope, NotUsed> EventsByPersistenceId(
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr)
            => EventsByPersistenceIdSource(
                persistenceId: persistenceId,
                fromSequenceNr: fromSequenceNr,
                toSequenceNr: toSequenceNr,
                refreshInterval: Option<(TimeSpan, IScheduler)>.Create(
                    (_readJournalConfig.RefreshInterval, _system.Scheduler)));

        public Source<EventEnvelope, NotUsed> EventsByTag(string tag, Offset offset)
            => EventsByTag(
                tag,
                offset is Sequence s
                    ? s.Value
                    : 0,
                null);

        public Source<string, NotUsed> PersistenceIds()
            => Source
                .Repeat(0L)
                .ConcatMany(
                    _ =>
                        _delaySource
                            .MapMaterializedValue(_ => NotUsed.Instance)
                            .ConcatMany(_ => CurrentPersistenceIds()))
                .StatefulSelectMany<string, string, NotUsed>(
                    () =>
                    {
                        var knownIds = ImmutableHashSet<string>.Empty;

                        IEnumerable<string> Next(string id)
                        {
                            var xs = ImmutableHashSet<string>.Empty.Add(id).Except(knownIds);
                            knownIds = knownIds.Add(id);
                            return xs;
                        }

                        return Next;
                    });

        private IImmutableList<IPersistentRepresentation> AdaptEvents(
            IPersistentRepresentation persistentRepresentation)
            => _eventAdapters
                .Get(persistentRepresentation.Payload.GetType())
                .FromJournal(persistentRepresentation.Payload, persistentRepresentation.Manifest)
                .Events
                .Select(persistentRepresentation.WithPayload)
                .ToImmutableList();

        private Source<EventEnvelope, NotUsed> EventsByPersistenceIdSource(
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            Option<(TimeSpan, IScheduler)> refreshInterval)
            => _readJournalDao
                .MessagesWithBatch(persistenceId, fromSequenceNr, toSequenceNr, _readJournalConfig.MaxBufferSize, refreshInterval)
                .SelectAsync(1, representationAndOrdering => Task.FromResult(representationAndOrdering.Get()))
                .SelectMany(r => AdaptEvents(r.Representation).Select(_ => new { representation = r.Representation, ordering = r.Ordering }))
                .Select(
                    r =>
                        new EventEnvelope(
                            offset: new Sequence(r.ordering),
                            persistenceId: r.representation.PersistenceId,
                            sequenceNr: r.representation.SequenceNr,
                            @event: r.representation.Payload,
                            timestamp: r.representation.Timestamp));

        private Source<EventEnvelope, NotUsed> CurrentJournalEvents(long offset, long max, MaxOrderingId latestOrdering)
        {
            if (latestOrdering.Max < offset)
                return Source.Empty<EventEnvelope>();

            return _readJournalDao
                .Events(offset, latestOrdering.Max, max)
                .SelectAsync(1, r => Task.FromResult(r.Get()))
                .SelectMany(
                    a =>
                    {
                        var (representation, _, ordering) = a;
                        return AdaptEvents(representation)
                            .Select(
                                r =>
                                    new EventEnvelope(
                                        offset: new Sequence(ordering),
                                        persistenceId: r.PersistenceId,
                                        sequenceNr: r.SequenceNr,
                                        @event: r.Payload,
                                        timestamp: r.Timestamp));
                    });
        }

        private Source<EventEnvelope, NotUsed> CurrentJournalEventsByTag(
            string tag,
            long offset,
            long max,
            MaxOrderingId latestOrdering)
        {
            if (latestOrdering.Max < offset)
                return Source.Empty<EventEnvelope>();

            return _readJournalDao
                .EventsByTag(tag, offset, latestOrdering.Max, max)
                .SelectAsync(1, r => Task.FromResult(r.Get()))
                .SelectMany(
                    a =>
                    {
                        var (representation, _, ordering) = a;
                        return AdaptEvents(representation)
                            .Select(
                                r =>
                                    new EventEnvelope(
                                        offset: new Sequence(ordering),
                                        persistenceId: r.PersistenceId,
                                        sequenceNr: r.SequenceNr,
                                        @event: r.Payload,
                                        timestamp: r.Timestamp));
                    });
        }

        private Source<EventEnvelope, NotUsed> EventsByTag(string tag, long offset, long? terminateAfterOffset)
        {
            var askTimeout = _readJournalConfig.JournalSequenceRetrievalConfiguration.AskTimeout;
            var batchSize = _readJournalConfig.MaxBufferSize;

            return Source
                .UnfoldAsync<(long offset, FlowControlEnum flowControl), IImmutableList<EventEnvelope>>(
                    (offset, FlowControlEnum.Continue),
                    uf =>
                    {
                        async Task<Option<((long, FlowControlEnum), IImmutableList<EventEnvelope>)>> RetrieveNextBatch()
                        {
                            var queryUntil = await _journalSequenceActor
                                .Ask<MaxOrderingId>(
                                    GetMaxOrderingId.Instance,
                                    askTimeout);

                            var xs = await CurrentJournalEventsByTag(tag, uf.offset, batchSize, queryUntil)
                                .RunWith(Sink.Seq<EventEnvelope>(), _mat);

                            var hasMoreEvents = xs.Count == batchSize;

                            var nextControl = FlowControlEnum.Unknown;
                            if (terminateAfterOffset.HasValue)
                            {
                                if (!hasMoreEvents && terminateAfterOffset.Value <= queryUntil.Max)
                                    nextControl = FlowControlEnum.Stop;

                                if (xs.Exists(r => r.Offset is Sequence s && s.Value >= terminateAfterOffset.Value))
                                    nextControl = FlowControlEnum.Stop;
                            }

                            if (nextControl == FlowControlEnum.Unknown)
                            {
                                nextControl = hasMoreEvents
                                    ? FlowControlEnum.Continue
                                    : FlowControlEnum.ContinueDelayed;
                            }

                            var nextStartingOffset = xs.Count == 0
                                ? Math.Max(uf.offset, queryUntil.Max)
                                : xs.Select(r => r.Offset as Sequence)
                                    .Where(r => r != null)
                                    .Max(t => t.Value);

                            return Option<((long, FlowControlEnum), IImmutableList<EventEnvelope>)>.Create(
                                ((nextStartingOffset, nextControl), xs));
                        }

                        return uf.flowControl switch
                        {
                            FlowControlEnum.Stop =>
                                Task.FromResult(
                                    Option<((long, FlowControlEnum), IImmutableList<EventEnvelope>)>.None),

                            FlowControlEnum.Continue =>
                                RetrieveNextBatch(),

                            FlowControlEnum.ContinueDelayed =>
                                FutureTimeoutSupport.After(
                                    duration: _readJournalConfig.RefreshInterval,
                                    scheduler: _system.Scheduler,
                                    value: RetrieveNextBatch),

                            _ => Task.FromResult(
                                Option<((long, FlowControlEnum), IImmutableList<EventEnvelope>)>.None),
                        };
                    }).SelectMany(r => r);
        }

        private Source<EventEnvelope, NotUsed> CurrentEventsByTag(string tag, long offset)
            => AsyncSource<long>
                .FromEnumerable(
                    state: new { readJournalDao = _readJournalDao },
                    func: async input => new[] { await input.readJournalDao.MaxJournalSequenceAsync() })
                .ConcatMany(maxInDb => EventsByTag(tag, offset, maxInDb));

        private Source<EventEnvelope, NotUsed> Events(long offset, long? terminateAfterOffset)
        {
            var askTimeout = _readJournalConfig.JournalSequenceRetrievalConfiguration.AskTimeout;
            var batchSize = _readJournalConfig.MaxBufferSize;

            return Source
                .UnfoldAsync<(long offset, FlowControlEnum flowControl), IImmutableList<EventEnvelope>>(
                    (offset, FlowControlEnum.Continue),
                    uf =>
                    {
                        async Task<Option<((long, FlowControlEnum), IImmutableList<EventEnvelope>)>> RetrieveNextBatch()
                        {
                            var queryUntil = await _journalSequenceActor
                                .Ask<MaxOrderingId>(
                                    GetMaxOrderingId.Instance,
                                    askTimeout);

                            var xs = await CurrentJournalEvents(uf.offset, batchSize, queryUntil)
                                .RunWith(Sink.Seq<EventEnvelope>(), _mat);

                            var hasMoreEvents = xs.Count == batchSize;

                            var nextControl = FlowControlEnum.Unknown;
                            if (terminateAfterOffset.HasValue)
                            {
                                if (!hasMoreEvents && terminateAfterOffset.Value <= queryUntil.Max)
                                    nextControl = FlowControlEnum.Stop;

                                if (xs.Exists(r => r.Offset is Sequence s && s.Value >= terminateAfterOffset.Value))
                                    nextControl = FlowControlEnum.Stop;
                            }

                            if (nextControl == FlowControlEnum.Unknown)
                            {
                                nextControl = hasMoreEvents
                                    ? FlowControlEnum.Continue
                                    : FlowControlEnum.ContinueDelayed;
                            }

                            var nextStartingOffset = xs.Count == 0
                                ? Math.Max(uf.offset, queryUntil.Max)
                                : xs.Select(r => r.Offset as Sequence)
                                    .Where(r => r != null)
                                    .Max(t => t.Value);

                            return Option<((long nextStartingOffset, FlowControlEnum nextControl), IImmutableList<EventEnvelope>xs)>.Create(
                                ((nextStartingOffset, nextControl), xs));
                        }

                        return uf.flowControl switch
                        {
                            FlowControlEnum.Stop =>
                                Task.FromResult(Option<((long, FlowControlEnum), IImmutableList<EventEnvelope>)>.None),

                            FlowControlEnum.Continue =>
                                RetrieveNextBatch(),

                            FlowControlEnum.ContinueDelayed =>
                                FutureTimeoutSupport.After(
                                    _readJournalConfig.RefreshInterval,
                                    _system.Scheduler,
                                    RetrieveNextBatch),

                            _ => Task.FromResult(
                                Option<((long, FlowControlEnum), IImmutableList<EventEnvelope>)>.None),
                        };
                    }).SelectMany(r => r);
        }
    }
}
