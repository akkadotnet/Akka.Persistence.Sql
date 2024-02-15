// -----------------------------------------------------------------------
//  <copyright file="BaseByteReadArrayJournalDao.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Extensions;
using Akka.Persistence.Sql.Journal.Dao;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Serialization;
using Akka.Persistence.Sql.Utility;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;
using LinqToDB;

namespace Akka.Persistence.Sql.Query.Dao
{
    public abstract class BaseByteReadArrayJournalDao : BaseJournalDaoWithReadMessages, IReadJournalDao
    {
        private readonly Flow<JournalRow, Try<(IPersistentRepresentation, IImmutableSet<string>, long)>, NotUsed> _deserializeFlow;

        private readonly ReadJournalConfig _readJournalConfig;
        private readonly DbStateHolder _dbStateHolder;

        protected BaseByteReadArrayJournalDao(
            IAdvancedScheduler scheduler,
            IMaterializer materializer,
            AkkaPersistenceDataConnectionFactory connectionFactory,
            ReadJournalConfig readJournalConfig,
            FlowPersistentRepresentationSerializer<JournalRow> serializer,
            CancellationToken shutdownToken)
            : base(scheduler, materializer, connectionFactory, readJournalConfig, shutdownToken)
        {
            _readJournalConfig = readJournalConfig;
            _dbStateHolder = new DbStateHolder(connectionFactory, ReadIsolationLevel, ShutdownToken, _readJournalConfig.PluginConfig.TagMode);
            _deserializeFlow = serializer.DeserializeFlow();
        }

        public Source<string, NotUsed> AllPersistenceIdsSource(long max)
        {
            var maxTake = MaxTake(max);

            return AsyncSource<string>.FromEnumerable(
                new { _dbStateHolder, maxTake },
                static async input =>
                {
                    return await input._dbStateHolder.ExecuteWithTransactionAsync(
                        input.maxTake,
                        async (connection, token,take) =>
                        {
                            return await connection
                                .GetTable<JournalRow>()
                                .Where(r => r.Deleted == false)
                                .Select(r => r.PersistenceId)
                                .Distinct()
                                .Take(take)
                                .ToListAsync(token);
                        });
                });
        }

        public Source<Try<(IPersistentRepresentation, IImmutableSet<string>, long)>, NotUsed> EventsByTag(
            string tag,
            long offset,
            long maxOffset,
            long max)
        {
            var separator = _readJournalConfig.PluginConfig.TagSeparator;
            var maxTake = MaxTake(max);

            return _readJournalConfig.PluginConfig.TagMode switch
            {
                TagMode.Csv => AsyncSource<JournalRow>
                    .FromEnumerable(
                        new { args= new QueryArgs(offset,maxOffset,maxTake,
                            $"{separator}{tag}{separator}"), _dbStateHolder },
                        static async input =>
                        {
                            //var tagValue = input.tag;
                            return await input._dbStateHolder.ExecuteWithTransactionAsync(
                                input.args,
                                static async (connection, token, inVals) =>
                                {
                                    return await connection
                                        .GetTable<JournalRow>()
                                        .Where(
                                            r =>
                                                r.Tags.Contains(inVals.Tag) &&
                                                !r.Deleted &&
                                                r.Ordering > inVals.Offset &&
                                                r.Ordering <= inVals.MaxOffset)
                                        .OrderBy(r => r.Ordering)
                                        .Take(inVals.Max)
                                        .ToListAsync(token);
                                });
                        })
                    .Via(_deserializeFlow),

                TagMode.TagTable => AsyncSource<JournalRow>
                    .FromEnumerable(
                        new { _dbStateHolder, args= new QueryArgs(offset,maxOffset,maxTake,tag)},
                        static async input =>
                        {
                            return await input._dbStateHolder.ExecuteWithTransactionAsync(
                                input.args,
                                static async (connection, token,txInput) =>
                                {
                                    var query =
                                        from r in connection.GetTable<JournalRow>()
                                        from lp in connection.GetTable<JournalTagRow>()
                                            .Where(jtr => jtr.OrderingId == r.Ordering).DefaultIfEmpty()
                                        where lp.OrderingId > txInput.Offset &&
                                              lp.OrderingId <= txInput.MaxOffset &&
                                              !r.Deleted &&
                                              lp.TagValue == txInput.Tag
                                        orderby r.Ordering
                                        select r;
                                    return await AddTagDataFromTagTableAsync(query.Take(txInput.Max), connection, token);
                                });
                        })
                    .Via(_deserializeFlow),

                _ => throw new ArgumentOutOfRangeException($"TagMode {_readJournalConfig.PluginConfig.TagMode} is not supported for read journals"),
            };
        }

        public override Task<Source<Try<ReplayCompletion>, NotUsed>> Messages(
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            long max)
            => Task.FromResult(
                AsyncSource<JournalRow>
                    .FromEnumerable(
                        new {  persistenceId, fromSequenceNr, toSequenceNr, toTake = MaxTake(max), _dbStateHolder },
                        static async state =>
                        {
                            return await state._dbStateHolder.ExecuteWithTransactionAsync(
                                state,
                                async (connection, token, txState) =>
                                {
                                    var query = connection
                                        .GetTable<JournalRow>()
                                        .Where(
                                            r =>
                                                r.PersistenceId == txState.persistenceId &&
                                                r.SequenceNumber >= txState.fromSequenceNr &&
                                                r.SequenceNumber <= txState.toSequenceNr &&
                                                r.Deleted == false)
                                        .OrderBy(r => r.SequenceNumber)
                                        .Take(txState.toTake);

                                    return await AddTagDataIfNeededAsync(txState._dbStateHolder.Mode, query, connection, token);
                                });
                        })
                    .Via(_deserializeFlow)
                    .Select(
                        t =>
                        {
                            try
                            {
                                var (representation, _, ordering) = t.Get();
                                return new Try<ReplayCompletion>(new ReplayCompletion(representation, ordering));
                            }
                            catch (Exception e)
                            {
                                return new Try<ReplayCompletion>(e);
                            }
                        }));

        public Source<long, NotUsed> JournalSequence(long offset, long limit)
        {
            return AsyncSource<long>.FromEnumerable(
                new { maxTake = MaxTake(limit), offset, _dbStateHolder },
                async input =>
                {
                    return await input._dbStateHolder.ExecuteWithTransactionAsync(
                        new QueryArgs(input.offset,default,input.maxTake, default),
                        static async (connection, token, args) =>
                        {
                            // persistence-jdbc does not filter deleted here.
                            return await connection
                                .GetTable<JournalRow>()
                                .Where(r => r.Ordering > args.Offset)
                                .Select(r => r.Ordering)
                                .OrderBy(r => r)
                                .Take(args.Max)
                                .ToListAsync(token);
                        }
                    );
                });
        }

        public async Task<long> MaxJournalSequenceAsync()
        {
            return await ConnectionFactory.ExecuteWithTransactionAsync(
                ReadIsolationLevel,
                ShutdownToken,
                async (connection, token) =>
                {
                    // persistence-jdbc does not filter deleted here.
                    var result = await connection
                        .GetTable<JournalRow>()
                        .MaxAsync<JournalRow, long?>(r => r.Ordering, token);

                    return result ?? 0;
                });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int MaxTake(long max)
            => max > int.MaxValue
                ? int.MaxValue
                : (int)max;


        public Source<Try<(IPersistentRepresentation, IImmutableSet<string>, long)>, NotUsed> Events(
            long offset,
            long maxOffset,
            long max)
        {
            var maxTake = MaxTake(max);

            return AsyncSource<JournalRow>.FromEnumerable(
                new {_dbStateHolder , args=new QueryArgs(offset,maxOffset,maxTake) },
                static async input =>
                {
                    return await ExecuteEventQuery(input._dbStateHolder, input._dbStateHolder.Mode, input.args);
                }
            ).Via(_deserializeFlow);
        }
        
        
        internal static async Task<List<JournalRow>> ExecuteEventQuery(DbStateHolder stateHolder, TagMode tagMode, QueryArgs queryArgs)
        {
            return tagMode != TagMode.TagTable
                ? await ExecuteEventQueryNonTagTable(stateHolder, queryArgs)
                : await ExecuteEventQueryTagTable(stateHolder, queryArgs);
        }

        private static async Task<List<JournalRow>> ExecuteEventQueryTagTable(DbStateHolder stateHolder, QueryArgs queryArgs)
        {
            return await stateHolder.ExecuteWithTransactionAsync(
                queryArgs,
                static async (connection, token, a) =>
                {
                    var query = connection
                        .GetTable<JournalRow>()
                        .Where(
                            r =>
                                r.Ordering > a.Offset &&
                                r.Ordering <= a.MaxOffset &&
                                r.Deleted == false)
                        .OrderBy(r => r.Ordering)
                        .Take(a.Max);
                    return await AddTagDataFromTagTableAsync(query, connection, token);
                });
        }

        private static async Task<List<JournalRow>> ExecuteEventQueryNonTagTable(DbStateHolder stateHolder, QueryArgs queryArgs)
        {
            return await stateHolder.ExecuteWithTransactionAsync(
                queryArgs,
                static async (connection, token, a) =>
                {
                    return await connection
                        .GetTable<JournalRow>()
                        .Where(
                            r =>
                                r.Ordering > a.Offset &&
                                r.Ordering <= a.MaxOffset &&
                                r.Deleted == false)
                        .OrderBy(r => r.Ordering)
                        .Take(a.Max)
                        .ToListAsync(token);
                });
        }

        private static async Task<List<JournalRow>> AddTagDataIfNeededAsync(
            TagMode mode, 
            IQueryable<JournalRow> rowQuery,
            AkkaDataConnection connection, 
            CancellationToken token
            )
        {
            if (mode != TagMode.TagTable)
                return await rowQuery.ToListAsync(token);
            return await AddTagDataFromTagTableAsync(rowQuery, connection, token);
        }

        private static async Task<List<JournalRow>> AddTagDataFromTagTableAsync(IQueryable<JournalRow> rowQuery, AkkaDataConnection connection, CancellationToken token)
        {
            var tagTable = connection.GetTable<JournalTagRow>();

            var rowsAndTags = await rowQuery
                .Select(
                    x => new
                    {
                        row = x,
                        tags = tagTable
                            .Where(r => r.OrderingId == x.Ordering)
                            .StringAggregate(";", r => r.TagValue)
                            .ToValue(),
                    })
                .ToListAsync(token);

            var result = new List<JournalRow>();
            foreach (var rowAndTags in rowsAndTags)
            {
                rowAndTags.row.TagArray = rowAndTags.tags?.Split(';') ?? Array.Empty<string>();
                result.Add(rowAndTags.row);
            }

            return result;
        }
    }
}
