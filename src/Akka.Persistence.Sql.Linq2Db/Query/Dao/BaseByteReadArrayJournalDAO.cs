using System;
using System.Collections.Immutable;
using System.Linq;
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
                    
                    return await conn.GetTable<JournalRow>()
                        .OrderBy(r => r.Ordering)
                        .Where(r => 
                            r.Ordering > input.offset && 
                            r.Ordering <= input.maxOffset &&
                            r.Deleted == false)
                        .Take(input.maxTake).ToListAsync();
                }
            ).Via(_deserializeFlow);
        }
        
        public Source<Try<(IPersistentRepresentation, IImmutableSet<string>, long)>, NotUsed> EventsByTag(
            string tag,
            long offset,
            long maxOffset,
            long max)
        {
            var separator = _readJournalConfig.PluginConfig.TagSeparator;
            var maxTake = MaxTake(max);
            
            return AsyncSource<JournalRow>.FromEnumerable(
                new{ separator, tag, offset, maxOffset, maxTake, _connectionFactory = ConnectionFactory},
                async input =>
                {
                    var tagValue = $"{separator}{input.tag}{separator}";
                    await using var conn = input._connectionFactory.GetConnection();
                    
                    return await conn.GetTable<JournalRow>()
                        .Where(r => 
                            r.Tags.Contains(tagValue) &&
                            r.Deleted == false)
                        .OrderBy(r => r.Ordering)
                        .Where(r => r.Ordering > input.offset && r.Ordering <= input.maxOffset)
                        .Take(input.maxTake).ToListAsync();
                })
                .Via(_deserializeFlow);
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
                    
                    return await conn.GetTable<JournalRow>()
                        .Where(r => r.Ordering > input.offset)
                        .Select(r => r.Ordering)
                        .OrderBy(r => r).Take(input.maxTake).ToListAsync();
                });
        }

        public async Task<long> MaxJournalSequenceAsync()
        {
            await using var db = ConnectionFactory.GetConnection();
            
            return await db.GetTable<JournalRow>()
                .Select(r => r.Ordering)
                .OrderByDescending(r => r)
                .FirstOrDefaultAsync();
        }
    }
}