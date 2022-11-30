using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Db;
using Akka.Persistence.Sql.Linq2Db.Journal.Dao;
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
    
    public abstract class BaseByteReadArrayJournalDao : BaseJournalDaoWithReadMessages, IReadJournalDao
    {
        private readonly ReadJournalConfig _readJournalConfig;
        private readonly FlowPersistentReprSerializer<JournalRow> _serializer;
        private readonly Flow<JournalRow, Try<(IPersistentRepresentation, IImmutableSet<string>, long)>, NotUsed> _deserializeFlow;

        protected BaseByteReadArrayJournalDao(
            IAdvancedScheduler scheduler,
            IMaterializer materializer,
            AkkaPersistenceDataConnectionFactory connectionFactory,
            ReadJournalConfig readJournalConfig,
            FlowPersistentReprSerializer<JournalRow> serializer) 
            : base(scheduler, materializer, connectionFactory)
        {
            _readJournalConfig = readJournalConfig;
            _serializer = serializer;
            _deserializeFlow = _serializer.DeserializeFlow();
        }

        public Source<string, NotUsed> AllPersistenceIdsSource(long max)
        {
            var maxTake = MaxTake(max);

            return AsyncSource<string>.FromEnumerable(
                new { _connectionFactory = ConnectionFactory, maxTake},
                async input =>
                {
                    await using var db = input._connectionFactory.GetConnection();
                    
                    return await db.GetTable<JournalRow>()
                        .Where(r => r.Deleted == false)
                        .Select(r => r.PersistenceId)
                        .Distinct()
                        .Take(input.maxTake).ToListAsync();
                });
        }

        private static int MaxTake(long max)
        {
            return max > int.MaxValue ? int.MaxValue : (int)max;
        }
        
        public Source<Try<(IPersistentRepresentation, IImmutableSet<string>, long)>, NotUsed> Events(
            long offset,
            long maxOffset,
            long max)
        {
            var maxTake = MaxTake(max);
            
            return AsyncSource<JournalRow>.FromEnumerable(
                new { _connectionFactory = ConnectionFactory, maxTake, maxOffset, offset},
                async input=>
                {
                    await using var conn = input._connectionFactory.GetConnection();
                    var events = await conn.GetTable<JournalRow>()
                        .OrderBy(r => r.Ordering)
                        .Where(r => 
                            r.Ordering > input.offset && 
                            r.Ordering <= input.maxOffset &&
                            r.Deleted == false)
                        .Take(input.maxTake).ToListAsync();
                    return await AddTagDataIfNeeded(events, conn);
                }
            ).Via(_deserializeFlow);
        }
        
        public async Task<List<JournalRow>> AddTagDataIfNeeded(List<JournalRow> toAdd, DataConnection context)
        {
            if ((_readJournalConfig.PluginConfig.TagReadMode & TagReadMode.TagTable) != 0)
            {
                await AddTagDataFromTagTable(toAdd, context);
            }
            return toAdd;
        }

        private async Task AddTagDataFromTagTable(List<JournalRow> toAdd, DataConnection context)
        {
            var pred = TagCheckPredicate(toAdd);
            var tagRows = pred.HasValue 
                ? await context.GetTable<JournalTagRow>().Where(pred.Value).ToListAsync()
                : new List<JournalTagRow>();
            if (_readJournalConfig.TableConfig.TagTableMode == TagTableMode.OrderingId)
            {
                foreach (var journalRow in toAdd)
                {
                    journalRow.TagArr = tagRows
                        .Where(r => r.JournalOrderingId == journalRow.Ordering)
                        .Select(r => r.TagValue).ToArray();
                }
            }
            else
            {
                foreach (var journalRow in toAdd)
                {
                    journalRow.TagArr = tagRows
                        .Where(r => r.WriteUuid == journalRow.WriteUuid)
                        .Select(r => r.TagValue).ToArray();
                }
            }
        }

        public Option<Expression<Func<JournalTagRow, bool>>> TagCheckPredicate(
            List<JournalRow> toAdd)
        {
            if (_readJournalConfig.PluginConfig.TagTableMode == TagTableMode.SequentialUuid)
            {
                //Check whether we have anything to query for two reasons:
                //1: Linq2Db may choke on an empty 'in' set.
                //2: Don't wanna make a useless round trip to the DB,
                //   if we know nothing is tagged.
                var set = toAdd
                    .Where(r => r.WriteUuid.HasValue)
                    .Select(r => r.WriteUuid.Value).ToList();
                return set.Count == 0 
                    ? Option<Expression<Func<JournalTagRow, bool>>>.None 
                    : new Option<Expression<Func<JournalTagRow, bool>>>(r => r.WriteUuid.In(set));
            }

            //We can just check the count here.
            //Alas, we won't know if there are tags
            //Until we actually query on this one.
            return toAdd.Count == 0 
                ? Option<Expression<Func<JournalTagRow, bool>>>.None 
                : new Option<Expression<Func<JournalTagRow, bool>>>( r => r.JournalOrderingId.In(toAdd.Select(r => r.Ordering)));
        }
        
        public Source<Try<(IPersistentRepresentation, IImmutableSet<string>, long)>, NotUsed> EventsByTag(
            string tag,
            long offset,
            long maxOffset,
            long max)
        {
            var separator = _readJournalConfig.PluginConfig.TagSeparator;
            var maxTake = MaxTake(max);
            switch (_readJournalConfig.PluginConfig.TagReadMode)
            {
                case TagReadMode.CommaSeparatedArray:
                    return AsyncSource<JournalRow>.FromEnumerable(
                            new { separator, tag, offset, maxOffset, maxTake, ConnectionFactory },
                            async input =>
                            {
                                var tagValue = $"{separator}{input.tag}{separator}";
                                await using var conn = input.ConnectionFactory.GetConnection();
                                
                                return await conn.GetTable<JournalRow>()
                                    .Where(r => r.Tags.Contains(tagValue))
                                    .OrderBy(r => r.Ordering)
                                    .Where(r => r.Ordering > input.offset && r.Ordering <= input.maxOffset)
                                    .Take(input.maxTake).ToListAsync();
                            })
                        .Via(_deserializeFlow);
                case TagReadMode.CommaSeparatedArrayAndTagTable:
                    return EventByTagMigration(tag, offset, maxOffset, separator, maxTake);
                case TagReadMode.TagTable:
                    return EventByTagTableOnly(tag, offset, maxOffset, separator, maxTake);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private Source<Try<(IPersistentRepresentation, IImmutableSet<string>, long)>, NotUsed> EventByTagTableOnly(
            string tag,
            long offset,
            long maxOffset,
            string separator,
            int maxTake)
        {
            return AsyncSource<JournalRow>
                .FromEnumerable(
                    new { ConnectionFactory, separator, tag, offset, maxOffset, maxTake },
                    async input =>
                    {
                        //TODO: Optimize Flow
                        await using var conn = input.ConnectionFactory.GetConnection();
                        //First, Get eligible rows.
                        var mainRows = await conn.GetTable<JournalRow>()
                                .LeftJoin(
                                    conn.GetTable<JournalTagRow>(),
                                    EventsByTagOnlyJoinPredicate,
                                    (jr, jtr) => new { jr, jtr })
                                .Where(r => r.jtr.TagValue == input.tag)
                                .Select(r => r.jr)
                                .Where(r => r.Ordering > input.offset && r.Ordering <= input.maxOffset)
                                .Take(input.maxTake).ToListAsync();
                        await AddTagDataFromTagTable(mainRows, conn);
                        return mainRows;
                    })
                //We still PerfectlyMatchTag here
                //Because DB Collation :)
                .Via(PerfectlyMatchTag(tag, separator))
                .Via(_deserializeFlow);
        }

        private Expression<Func<JournalRow, JournalTagRow, bool>> EventsByTagOnlyJoinPredicate
        {
            get
            {
                if (_readJournalConfig.TableConfig.TagTableMode == TagTableMode.OrderingId)
                    return (jr, jtr) => jr.Ordering == jtr.JournalOrderingId;
                
                return (jr, jtr) => jr.WriteUuid == jtr.WriteUuid;
            }
        }
        
        private Source<Try<(IPersistentRepresentation, IImmutableSet<string>, long)>, NotUsed> EventByTagMigration(
            string tag,
            long offset,
            long maxOffset,
            string separator,
            int maxTake)
        {
            return AsyncSource<JournalRow>.FromEnumerable(
                    new { ConnectionFactory, separator, tag, offset, maxOffset, maxTake },
                    async input =>
                    {
                        // NOTE: This flow is probably not performant,
                        // It is meant to allow for safe migration
                        // And is not necessarily intended for long term use
                        await using var conn = input.ConnectionFactory.GetConnection();
                        
                        // First, find the rows.
                        // We use IN here instead of left join because it's safer from a
                        // 'avoid duplicate rows tripping things up later' standpoint.
                        var tagValue = $"{separator}{input.tag}{separator}";
                        var mainRows = await conn.GetTable<JournalRow>()
                            .Where(EventsByTagMigrationPredicate(conn, input.tag))
                            .OrderBy(r => r.Ordering)
                            .Where(r => r.Ordering > input.offset && r.Ordering <= input.maxOffset && r.Deleted == false)
                            .Take(input.maxTake).ToListAsync();
                        
                        await AddTagDataFromTagTable(mainRows, conn);
                        return mainRows;
                    })
                .Via(PerfectlyMatchTag(tag, separator))
                .Via(_deserializeFlow);
        }        
        
        private Expression<Func<JournalRow, bool>> EventsByTagMigrationPredicate(DataConnection conn, string tagVal)
        {
            if (_readJournalConfig.TableConfig.TagTableMode == TagTableMode.OrderingId)
            {
                return r => r.Ordering
                                .In(conn.GetTable<JournalTagRow>()
                                    .Where(j => j.TagValue == tagVal)
                                    .Select(j => j.JournalOrderingId))
                              || r.Tags.Contains(tagVal);
            }
            return r => r.WriteUuid.Value
                            .In(conn.GetTable<JournalTagRow>()
                                .Where(j => j.TagValue == tagVal)
                                .Select(j => j.WriteUuid))
                        || r.Tags.Contains(tagVal);
        }

        private Flow<JournalRow, JournalRow, NotUsed> PerfectlyMatchTag(
            string tag,
            string separator)
        {
            //Do the tagArr check first here
            //Since the logic is simpler.
            return Flow.Create<JournalRow>()
                .Where(r => r.TagArr?.Contains(tag) ?? (r.Tags ?? "")
                    .Split( new[] { separator }, StringSplitOptions.RemoveEmptyEntries )
                    .Any(t => t.Contains(tag)));
        }

        public override Source<Try<ReplayCompletion>, NotUsed> Messages(
            DataConnection dc, 
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            long max)
        {
            return AsyncSource<JournalRow>.FromEnumerable(
                new { dc, persistenceId, fromSequenceNr, toSequenceNr, toTake = MaxTake(max) }, 
                async state =>
                    await dc.GetTable<JournalRow>()
                        .Where(r => 
                            r.PersistenceId == state.persistenceId 
                            && r.SequenceNumber >= state.fromSequenceNr
                            && r.SequenceNumber <= state.toSequenceNr
                            && r.Deleted == false)
                        .OrderBy(r => r.SequenceNumber)
                        .Take(state.toTake).ToListAsync())
                .Via(_deserializeFlow)
                .Select( t =>
                {
                    try
                    {
                        var (representation, _, ordering) = t.Get();
                        return new Try<ReplayCompletion>(new ReplayCompletion(representation ,ordering));
                    }
                    catch (Exception e)
                    {
                        return new Try<ReplayCompletion>(e);
                    }
                });
        }

        public Source<long, NotUsed> JournalSequence(long offset, long limit)
        {
            var maxTake = MaxTake(limit);
            return AsyncSource<long>.FromEnumerable(
                new { maxTake, offset, _connectionFactory = ConnectionFactory },
                async input =>
                {
                    await using var conn = input._connectionFactory.GetConnection();
                    
                    //persistence-jdbc does not filter deleted here.
                    return await conn.GetTable<JournalRow>()
                        .Where(r => r.Ordering > input.offset)
                        .Select(r => r.Ordering)
                        .OrderBy(r => r).Take(input.maxTake).ToListAsync();
                });
        }

        public async Task<long> MaxJournalSequenceAsync()
        {
            await using var db = ConnectionFactory.GetConnection();
            
            //persistence-jdbc does not filter deleted here.
            return await db.GetTable<JournalRow>()
                .Select(r => r.Ordering)
                .OrderByDescending(r => r)
                .FirstOrDefaultAsync();
        }
    }
}