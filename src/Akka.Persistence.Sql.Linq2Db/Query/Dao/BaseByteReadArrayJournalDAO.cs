using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
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
            
            return AsyncSource<JournalRow>.FromEnumerable(new {t=this,maxTake,maxOffset,offset},async(input)=>
                {
                    using (var conn = input.t._connectionFactory.GetConnection())
                    {
                        var evts =  await  input.t.baseQuery(conn)
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
                await addTagDataFromTagTable(toAdd, context);
            }
            return toAdd;
        }

        private async Task addTagDataFromTagTable(List<JournalRow> toAdd, DataConnection context)
        {
            var pred = TagCheckPredicate(toAdd);
            var tagRows = pred.HasValue
                ? await context.GetTable<JournalTagRow>()
                    .Where(pred.Value)
                    .ToListAsync()
                : new List<JournalTagRow>();
            if (_readJournalConfig.TableConfig.TagTableMode ==
                TagTableMode.OrderingId)
            {
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
            else
            {
                foreach (var journalRow in toAdd)
                {
                    journalRow.tagArr =
                        tagRows.Where(r =>
                            r.WriteUUID ==
                            journalRow.WriteUUID)
                        .Select(r => r.TagValue)
                        .ToArray();
                }
            }
        }

        public Option<Expression<Func<JournalTagRow, bool>>> TagCheckPredicate(
            List<JournalRow> toAdd)
        {
            if (_readJournalConfig.PluginConfig.TagTableMode ==
                TagTableMode.SequentialUUID)
            {
                //Check whether we have anything to query for two reasons:
                //1: Linq2Db may choke on an empty 'in' set.
                //2: Don't wanna make a useless round trip to the DB,
                //   if we know nothing is tagged.
                var set = toAdd.Where(r => r.WriteUUID.HasValue)
                    .Select(r => r.WriteUUID.Value).ToList();
                if (set.Count == 0)
                {
                    return Option<Expression<Func<JournalTagRow, bool>>>.None;
                }
                else
                {
                    return new Option<Expression<Func<JournalTagRow, bool>>>(r =>
                        r.WriteUUID.In(set));    
                }
            }
            else
            {
                //We can just check the count here.
                //Alas, we won't know if there are tags
                //Until we actually query on this one.
                if (toAdd.Count == 0)
                {
                    return Option<Expression<Func<JournalTagRow, bool>>>.None;
                }
                else
                {
                    return new Option<Expression<Func<JournalTagRow, bool>>>( r =>
                        r.JournalOrderingId.In(toAdd.Select(r => r.ordering)));    
                }
            }
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
                    return AsyncSource<JournalRow>.FromEnumerable(new{separator,tag,offset,maxOffset,maxTake,t=this},
                            async(input)=>
                            {
                                using (var conn = input.t._connectionFactory.GetConnection())
                                {
                                    return await input.t.baseQuery(conn)
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
                        separator, tag, offset, maxOffset, maxTake, t=this
                    },
                    async (input) =>
                    {
                        //TODO: Optimize Flow
                        using (var conn = input.t._connectionFactory.GetConnection())
                        {
                            //First, Get eligible rows.
                            var mainRows = await
                                input.t.baseQuery(conn)
                                    .LeftJoin(
                                        conn.GetTable<
                                            JournalTagRow>(),
                                        EventsByTagOnlyJoinPredicate,
                                        (jr, jtr) =>
                                            new { jr, jtr })
                                    .Where(r =>
                                        r.jtr.TagValue == input.tag)
                                    .Select(r => r.jr)
                                    .Where(r =>
                                        r.ordering > input.offset &&
                                        r.ordering <= input.maxOffset)
                                    .Take(input.maxTake).ToListAsync();
                            await addTagDataFromTagTable(mainRows, conn);
                            return mainRows;
                        }
                    })
                //We still PerfectlyMatchTag here
                //Because DB Collation :)
                .Via(perfectlyMatchTag(tag, separator))
                .Via(deserializeFlow);
        }

        private Expression<Func<JournalRow, JournalTagRow, bool>> EventsByTagOnlyJoinPredicate
        {
            get
            {
                if (_readJournalConfig.TableConfig
                        .TagTableMode ==
                    TagTableMode.OrderingId)
                    return (jr, jtr) =>
                        jr.ordering ==
                        jtr.JournalOrderingId;
                else
                    return (jr, jtr) =>
                        jr.WriteUUID == jtr.WriteUUID;
            }
        }

        private Source<Try<(IPersistentRepresentation, IImmutableSet<string>, long)>, NotUsed> eventByTagMigration(string tag, long offset, long maxOffset,
            string separator, int maxTake)
        {
            return AsyncSource<JournalRow>.FromEnumerable(
                    new
                    {
                        separator, tag, offset, maxOffset, maxTake, t =this
                    },
                    async (input) =>
                    {
                        //NOTE: This flow is probably not performant,
                        //It is meant to allow for safe migration
                        //And is not necessarily intended for long term use
                        using (var conn = input.t._connectionFactory.GetConnection())
                        {
                            //First, find the rows.
                            //We use IN here instead of leftjoin
                            //because it's safer from a
                            //'avoid duplicate rows tripping things up later'
                            //standpoint.
                            var mainRows = await input.t.baseQuery(conn)
                                .Where(
                                    eventsByTagMigrationPredicate(conn, input.tag)
                                )
                                .OrderBy(r => r.ordering)
                                .Where(r =>
                                    r.ordering > input.offset &&
                                    r.ordering <= input.maxOffset)
                                .Take(input.maxTake).ToListAsync();
                            await addTagDataFromTagTable(mainRows, conn);
                            return mainRows;
                        }
                    }).Via(perfectlyMatchTag(tag, separator))
                .Via(deserializeFlow);
        }

        private Expression<Func<JournalRow, bool>> eventsByTagMigrationPredicate(DataConnection conn, string tagVal)
        {
            if (_readJournalConfig.TableConfig.TagTableMode == TagTableMode.OrderingId)
            {
                return (JournalRow r) => r.ordering.In(
                                      conn.GetTable<
                                              JournalTagRow>().Where(r =>
                                              r.TagValue ==
                                              tagVal)
                                          .Select(r =>
                                              r.JournalOrderingId))
                                  || r.tags.Contains(tagVal);
            }
            else
            {
                return (JournalRow r) => r.WriteUUID.Value.In(
                                             conn.GetTable<
                                                     JournalTagRow>().Where(r =>
                                                     r.TagValue ==
                                                     tagVal)
                                                 .Select(r =>
                                                     r.WriteUUID))
                                         || r.tags.Contains(tagVal);
            }
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
                        //persistence-jdbc does not filter deleted here.
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
                //persistence-jdbc does not filter deleted here.
                return await db.GetTable<JournalRow>()
                    .Select<JournalRow, long>(r => r.ordering)
                    .OrderByDescending(r=>r)
                    .FirstOrDefaultAsync();
            }
        }
    }
}