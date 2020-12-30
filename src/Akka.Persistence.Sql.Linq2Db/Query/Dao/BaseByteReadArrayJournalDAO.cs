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

namespace Akka.Persistence.Sql.Linq2Db.Query.Dao
{
    
    public abstract class
        BaseByteReadArrayJournalDAO : BaseJournalDaoWithReadMessages,
            IReadJournalDAO
    {
        private bool includeDeleted;
        private ReadJournalConfig _readJournalConfig;
        private FlowPersistentReprSerializer<JournalRow> _serializer;

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
        }

        protected IQueryable<JournalRow> baseQuery(DataConnection connection)
        {
            return connection.GetTable<JournalRow>()
                .Where(jr =>
                    includeDeleted == false || (jr.deleted == false));
        }

        public Source<string, NotUsed> AllPersistenceIdsSource(long max)
        {
            
                var maxTake = MaxTake(max);
                
                return AsyncStreamSource.FromEnumerable<string>(async () =>
                {
                    using (var db = _connectionFactory.GetConnection())
                    {
                        return await baseQuery(db)
                            .Select(r => r.persistenceId)
                            .Distinct()
                            .Take(maxTake).ToListAsync();
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
            var separator = _readJournalConfig.PluginConfig.TagSeparator;
            var maxTake = MaxTake(max);
            
                
                return AsyncStreamSource.FromEnumerable<JournalRow>(async ()=>
                    {
                        using (var conn = _connectionFactory.GetConnection())
                        {
                            return await conn.GetTable<JournalRow>()
                                .OrderBy(r => r.ordering)
                                .Where(r =>
                                    r.ordering > offset &&
                                    r.ordering <= maxOffset)
                                .Take(maxTake).ToListAsync();
                        }
                    }
                ).Via(_serializer.DeserializeFlow());
            
        }
        
        public Source<
            Akka.Util.Try<(IPersistentRepresentation, IImmutableSet<string>, long)>,
            NotUsed> EventsByTag(string tag, long offset, long maxOffset,
            long max)
        {
            var separator = _readJournalConfig.PluginConfig.TagSeparator;
            var maxTake = MaxTake(max);
            
            return AsyncStreamSource.FromEnumerable<JournalRow>(async ()=>
                {
                    using (var conn = _connectionFactory.GetConnection())
                    {
                        return await conn.GetTable<JournalRow>()
                            .Where<JournalRow>(r => r.tags.Contains(tag))
                            .OrderBy(r => r.ordering)
                            .Where(r =>
                                r.ordering > offset && r.ordering <= maxOffset)
                            .Take(maxTake).ToListAsync();

                    }
                }).Via(perfectlyMatchTag(tag, separator))
                    .Via(_serializer.DeserializeFlow());
            
        }

        private Flow<JournalRow, JournalRow, NotUsed> perfectlyMatchTag(
            string tag,
            string separator)
        {

            return Flow.Create<JournalRow>().Where(r =>
                (r.tags ?? "")
                .Split(new[] {separator}, StringSplitOptions.RemoveEmptyEntries)
                .Any(t => t.Contains(tag)));
        }

        public override async Task<Source<Akka.Util.Try<ReplayCompletion>, NotUsed>> Messages(
            DataConnection dc, string persistenceId, long fromSequenceNr,
            long toSequenceNr, long max)
        {
            var toTake = MaxTake(max);
            return Source.From(
                        await baseQuery(dc)
                            .Where(r => r.persistenceId == persistenceId
                                        && r.sequenceNumber >= fromSequenceNr
                                        && r.sequenceNumber <= toSequenceNr)
                            .OrderBy(r => r.sequenceNumber)
                            .Take(toTake).ToListAsync())
                    .Via(_serializer.DeserializeFlow())
                    .Select(
                        t =>
                        {
                            try
                            {
                                var val = t.Get();
                                return new Akka.Util.Try<ReplayCompletion>(
                                    new ReplayCompletion()
                                    {
                                        repr = val.Item1, Ordering = val.Item3
                                    });
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
            return AsyncStreamSource.FromEnumerable<long>(async () =>
            {
                using (var conn = _connectionFactory.GetConnection())
                {
                    return await conn.GetTable<JournalRow>()
                        .Where<JournalRow>(r => r.ordering > offset)
                        .Select(r => r.ordering)
                        .OrderBy(r => r).Take(maxTake).ToListAsync();
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