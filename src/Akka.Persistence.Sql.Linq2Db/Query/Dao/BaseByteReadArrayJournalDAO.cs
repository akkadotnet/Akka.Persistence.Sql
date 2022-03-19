using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Db;
using Akka.Persistence.Sql.Linq2Db.Journal.DAO;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using Akka.Persistence.Sql.Linq2Db.Serialization;
using Akka.Persistence.Sql.Linq2Db.Utility;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Tools;

namespace Akka.Persistence.Sql.Linq2Db.Query.Dao
{
    
    public abstract class
        BaseByteReadArrayJournalDAO : BaseJournalDaoWithReadMessages,
            IReadJournalDAO
    {
        private bool includeDeleted;
        private ReadJournalConfig _readJournalConfig;
        private FlowPersistentReprSerializer<JournalRow> _serializer;
        private Flow<JournalRow, Try<(IPersistentRepresentation, IImmutableSet<string>, long)>, NotUsed> deserializeFlow;

        protected BaseByteReadArrayJournalDAO(IAdvancedScheduler ec,
            IMaterializer mat,
            AkkaPersistenceDataConnectionFactory connectionFactory,
            ReadJournalConfig readJournalConfig,
            FlowPersistentReprSerializer<JournalRow> serializer) : base(ec, mat,
            connectionFactory)
        {

            _readJournalConfig = readJournalConfig;
            includeDeleted = readJournalConfig.IncludeDeleted;
            _serializer = serializer;
            deserializeFlow = _serializer.DeserializeFlow();
        }

        protected IQueryable<JournalRow> baseQuery(DataConnection connection)
        {
            return connection.GetTable<JournalRow>()
                .Where(jr =>
                    includeDeleted == false || (jr.deleted == false));
        }
        
        protected static IQueryable<JournalRow> baseQueryStatic(DataConnection connection, bool includeDeleted)
        {
            return connection.GetTable<JournalRow>()
                .Where(jr =>
                    includeDeleted == false || (jr.deleted == false));
        }

        public Source<string, NotUsed> AllPersistenceIdsSource(long max)
        {
            
                var maxTake = MaxTake(max);

                return AsyncSource<string>.FromEnumerable(
                    new {_connectionFactory, maxTake, includeDeleted},
                    async (input) =>
                    {
                        using (var db =
                            input._connectionFactory.GetConnection())
                        {
                            return await baseQueryStatic(db,
                                    input.includeDeleted)
                                .Select(r => r.persistenceId)
                                .Distinct()
                                .Take(input.maxTake).ToListAsync();
                        }
                    });
        }

        private static int MaxTake(long max)
        {
            int maxTake;
            if (max > Int32.MaxValue)
            {
                maxTake = Int32.MaxValue;
            }
            else
            {
                maxTake = (int) max;
            }

            return maxTake;
        }
        
        public Source<
            Akka.Util.Try<(IPersistentRepresentation, IImmutableSet<string>, long)>,
            NotUsed> Events(long offset, long maxOffset,
            long max)
        {
            
            var maxTake = MaxTake(max);
            
            return AsyncSource<JournalRow>.FromEnumerable(new {_connectionFactory,maxTake,maxOffset,offset},async(input)=>
                {
                    using (var conn = input._connectionFactory.GetConnection())
                    {
                        var evts =  await conn.GetTable<JournalRow>()
                            .OrderBy(r => r.ordering)
                            .Where(r =>
                                r.ordering > input.offset &&
                                r.ordering <= input.maxOffset)
                            .Take(input.maxTake).ToListAsync();
                        return await AddTagDataIfNeeded(evts, conn);
                    }
                }
            ).Via(deserializeFlow);
             
            
        }

        public async Task<List<JournalRow>> AddTagDataIfNeeded(List<JournalRow> toAdd, DataConnection context)
        {
            if ((_readJournalConfig.PluginConfig.TagReadMode &
                TagReadMode.TagTable) != 0)
            {
                var tagRows = await context.GetTable<JournalTagRow>()
                    .Where(r =>
                        r.JournalOrderingId.In(toAdd.Select(r => r.ordering)))
                    .OrderBy(r=>r.JournalOrderingId)
                    .ToListAsync();
                foreach (var journalRow in toAdd)
                {
                    journalRow.tagArr =
                        tagRows.Where(r =>
                                r.JournalOrderingId ==
                                journalRow.ordering)
                            .Select(r => r.TagValue)
                            .ToArray();
                }
            }

            return toAdd;
        }
        public Source<
            Akka.Util.Try<(IPersistentRepresentation, IImmutableSet<string>, long)>,
            NotUsed> EventsByTag(string tag, long offset, long maxOffset,
            long max)
        {
            var separator = _readJournalConfig.PluginConfig.TagSeparator;
            var maxTake = MaxTake(max);
            switch (_readJournalConfig.PluginConfig.TagReadMode)
            {
                case TagReadMode.CommaSeparatedArray:
                    return AsyncSource<JournalRow>.FromEnumerable(new{separator,tag,offset,maxOffset,maxTake,_connectionFactory},
                            async(input)=>
                            {
                                using (var conn = input._connectionFactory.GetConnection())
                                {
                                    return await conn.GetTable<JournalRow>()
                                        .Where<JournalRow>(r => r.tags.Contains(input.tag))
                                        .OrderBy(r => r.ordering)
                                        .Where(r =>
                                            r.ordering > input.offset && r.ordering <= input.maxOffset)
                                        .Take(input.maxTake).ToListAsync();
                                }
                            }).Via(perfectlyMatchTag(tag, separator))
                        .Via(deserializeFlow);
                case TagReadMode.CommaSeparatedArrayAndTagTable:
                    return eventByTagMigration(tag, offset, maxOffset, separator, maxTake);
                case TagReadMode.TagTable:
                    return eventByTagTableOnly(tag, offset, maxOffset, separator, maxTake);
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            

        }

        private Source<
            Try<(IPersistentRepresentation, IImmutableSet<string>, long)>,
            NotUsed> eventByTagTableOnly(string tag, long offset,
            long maxOffset,
            string separator, int maxTake)
        {
            return AsyncSource<JournalRow>.FromEnumerable(
                    new
                    {
                        separator, tag, offset, maxOffset, maxTake, _connectionFactory
                    },
                    async (input) =>
                    {
                        //TODO: Optimize Flow
                        using (var conn = input._connectionFactory.GetConnection())
                        {
                            //First, Get eligible rows.
                            var mainRows = await 
                                    conn.GetTable<JournalRow>()
                                        .LeftJoin(
                                            conn.GetTable<
                                                JournalTagRow>(),
                                            (jr, jtr) =>
                                                jr.ordering ==
                                                jtr.JournalOrderingId,
                                            (jr, jtr) =>
                                                new { jr, jtr })
                                        .Where(r =>
                                            r.jtr.TagValue == input.tag)
                                        .Select(r=>r.jr)
                                        .Where(r =>
                                            r.ordering > input.offset &&
                                            r.ordering <= input.maxOffset)
                                        .Take(input.maxTake).ToListAsync();
                            //Then, Get the tags for the rows.
                            var tagRows = await conn.GetTable<JournalTagRow>()
                                .Where(r =>
                                    r.JournalOrderingId.In(
                                        mainRows.Select(r =>
                                            r.ordering)))
                                .ToListAsync();
                            foreach (var journalRow in mainRows)
                            {
                                journalRow.tagArr =
                                    tagRows.Where(r =>
                                            r.JournalOrderingId ==
                                            journalRow.ordering)
                                        .Select(r => r.TagValue)
                                        .ToArray();
                            }

                            return mainRows;
                        }
                    }).Via(perfectlyMatchTag(tag, separator))
                .Via(deserializeFlow);
        }

        private Source<Try<(IPersistentRepresentation, IImmutableSet<string>, long)>, NotUsed> eventByTagMigration(string tag, long offset, long maxOffset,
            string separator, int maxTake)
        {
            return AsyncSource<JournalRow>.FromEnumerable(
                    new
                    {
                        separator, tag, offset, maxOffset, maxTake, _connectionFactory
                    },
                    async (input) =>
                    {
                        //NOTE: This flow is probably not performant,
                        //It is meant to allow for safe migration
                        //And is not necessarily intended for long term use
                        using (var conn = input._connectionFactory.GetConnection())
                        {
                            //First, fin
                            var mainRows = await conn.GetTable<JournalRow>()
                                .Where(r => r.ordering.In(
                                    conn.GetTable<JournalRow>()
                                        .LeftJoin(
                                            conn.GetTable<
                                                JournalTagRow>(),
                                            (jr, jtr) =>
                                                jr.ordering ==
                                                jtr.JournalOrderingId,
                                            (jr, jtr) =>
                                                new { jr, jtr })
                                        .Where(r =>
                                            r.jr.tags.Contains(
                                                input.tag) ||
                                            r.jtr.TagValue == input.tag)
                                        .Select(r => r.jr.ordering)
                                        .OrderBy(r => r)
                                        .Where(r =>
                                            r > input.offset &&
                                            r <= input.maxOffset)
                                        .Take(input.maxTake))).ToListAsync();
                            var tagRows = await conn.GetTable<JournalTagRow>()
                                .Where(r =>
                                    r.JournalOrderingId.In(
                                        mainRows.Select(r =>
                                            r.ordering)))
                                .ToListAsync();
                            foreach (var journalRow in mainRows)
                            {
                                journalRow.tagArr =
                                    tagRows.Where(r =>
                                            r.JournalOrderingId ==
                                            journalRow.ordering)
                                        .Select(r => r.TagValue)
                                        .ToArray();
                            }

                            return mainRows;
                        }
                    }).Via(perfectlyMatchTag(tag, separator))
                .Via(deserializeFlow);
        }

        private Flow<JournalRow, JournalRow, NotUsed> perfectlyMatchTag(
            string tag,
            string separator)
        {
            //Do the tagArr check first here
            //Since the logic is simpler.
            return Flow.Create<JournalRow>().Where(r =>
                r.tagArr?.Contains(tag)??
                (r.tags ?? "")
                .Split(new[] {separator}, StringSplitOptions.RemoveEmptyEntries)
                .Any(t => t.Contains(tag)));
        }

        public override Source<Akka.Util.Try<ReplayCompletion>, NotUsed> Messages(
            DataConnection dc, string persistenceId, long fromSequenceNr,
            long toSequenceNr, long max)
        {
            return AsyncSource<JournalRow>.FromEnumerable(
                    new
                    {
                        dc, persistenceId, fromSequenceNr, toSequenceNr,toTake= MaxTake(max),
                        includeDeleted
                    }, async (state) =>

                        await baseQueryStatic(state.dc, state.includeDeleted)
                            .Where(r => r.persistenceId == state.persistenceId
                                        && r.sequenceNumber >=
                                        state.fromSequenceNr
                                        && r.sequenceNumber <=
                                        state.toSequenceNr)
                            .OrderBy(r => r.sequenceNumber)
                            .Take(state.toTake).ToListAsync())
                .Via(deserializeFlow)
                .Select(
                    t =>
                    {
                        try
                        {
                            var val = t.Get();
                            return new Akka.Util.Try<ReplayCompletion>(
                                new ReplayCompletion(val.Item1,val.Item3)
                                );
                        }
                        catch (Exception e)
                        {
                            return new Akka.Util.Try<ReplayCompletion>(e);
                        }
                    });


        }

        public Source<long, NotUsed> JournalSequence(long offset, long limit)
        {
            var maxTake = MaxTake(limit);
            return AsyncSource<long>.FromEnumerable(new {maxTake, offset, _connectionFactory},
                async (input) =>
                {
                    
                    using (var conn = input._connectionFactory.GetConnection())
                    {
                        return await conn.GetTable<JournalRow>()
                            .Where<JournalRow>(r => r.ordering > input.offset)
                            .Select(r => r.ordering)
                            .OrderBy(r => r).Take(input.maxTake).ToListAsync();
                    }
                });
        }

        public async Task<long> MaxJournalSequenceAsync()
        {
            using (var db = _connectionFactory.GetConnection())
            {
                return await db.GetTable<JournalRow>()
                    .Select<JournalRow, long>(r => r.ordering)
                    .FirstOrDefaultAsync();
            }
        }
    }
}