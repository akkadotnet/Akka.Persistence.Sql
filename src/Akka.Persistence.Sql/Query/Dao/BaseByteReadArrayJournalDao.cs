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
            _deserializeFlow = serializer.DeserializeFlow();
        }

        public Source<string, NotUsed> AllPersistenceIdsSource(long max)
        {
            var maxTake = MaxTake(max);

            return AsyncSource<string>.FromEnumerable(
                new { _connectionFactory = ConnectionFactory, maxTake },
                async input =>
                {
                    return await input._connectionFactory.ExecuteWithTransactionAsync(input.maxTake,
                        ReadIsolationLevel,
                        ShutdownToken,
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
                        new { separator, tag=$"{separator}{tag}{separator}", offset, maxOffset, maxTake, _connectionFactory = ConnectionFactory },
                        async input =>
                        {
                            //var tagValue = input.tag;
                            return await input._connectionFactory.ExecuteWithTransactionAsync(
                                input,
                                ReadIsolationLevel,
                                ShutdownToken,
                                static async (connection, token, inVals) =>
                                {
                                    return await connection
                                        .GetTable<JournalRow>()
                                        .Where(
                                            r =>
                                                r.Tags.Contains(inVals.tag) &&
                                                !r.Deleted &&
                                                r.Ordering > inVals.offset &&
                                                r.Ordering <= inVals.maxOffset)
                                        .OrderBy(r => r.Ordering)
                                        .Take(inVals.maxTake)
                                        .ToListAsync(token);
                                });
                        })
                    .Via(_deserializeFlow),

                TagMode.TagTable => AsyncSource<JournalRow>
                    .FromEnumerable(
                        new { inst=this, separator, tag, offset, maxOffset, maxTake },
                        static async input =>
                        {
                            var inst = input.inst;
                            return await inst.ConnectionFactory.ExecuteWithTransactionAsync(
                                input,
                                inst.ReadIsolationLevel,
                                inst.ShutdownToken,
                                static async (connection, token,txInput) =>
                                {
                                    /*
                                    var containedTags = connection.GetTable<JournalTagRow>()
                                        .Where(
                                            r =>
                                                r.OrderingId > txInput.offset &&
                                                r.OrderingId <= txInput.maxOffset &&
                                                r.TagValue == txInput.tag
                                        ).Select(r => r.OrderingId);
                                    var query = connection.GetTable<JournalRow>()
                                        .Where(r => r.Ordering.In(containedTags) && r.Deleted == false)
                                        .OrderBy(r => r.Ordering);
                                        */
                                    var query = connection.GetTable<JournalRow>()
                                        .Where(r => r.Deleted == false)
                                        .Join(
                                            connection.GetTable<JournalTagRow>()
                                                .Where(
                                                    jtr =>
                                                        jtr.OrderingId > txInput.offset
                                                        && jtr.OrderingId <= txInput.maxOffset
                                                        && jtr.TagValue == txInput.tag),
                                            SqlJoinType.Left,
                                            (jr, jtr) => (jr.Ordering == jtr.OrderingId),
                                            (jr, jtr) => jr)
                                        .OrderBy(r => r.Ordering)
                                        .Take(txInput.maxTake);
                                    return await AddTagDataFromTagTableAsync(query, connection, token);
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
                        new { _connectionFactory = ConnectionFactory, persistenceId, fromSequenceNr, toSequenceNr, toTake = MaxTake(max) },
                        async state =>
                        {
                            return await state._connectionFactory.ExecuteWithTransactionAsync(
                                state,
                                ReadIsolationLevel,
                                ShutdownToken,
                                async (connection, token,txState) =>
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

                                    return await AddTagDataIfNeededAsync(query, connection, token);
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
            var maxTake = MaxTake(limit);

            return AsyncSource<long>.FromEnumerable(
                new { maxTake, offset, _connectionFactory = ConnectionFactory },
                async input =>
                {
                    return await input._connectionFactory.ExecuteWithTransactionAsync(
                        ReadIsolationLevel,
                        ShutdownToken,
                        async (connection, token) =>
                        {
                            // persistence-jdbc does not filter deleted here.
                            return await connection
                                .GetTable<JournalRow>()
                                .Where(r => r.Ordering > input.offset)
                                .Select(r => r.Ordering)
                                .OrderBy(r => r)
                                .Take(input.maxTake)
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

        public readonly struct QueryArgs
        {
            public readonly long Offset;
            public readonly long MaxOffset;
            public readonly int Max;

            public QueryArgs(long offset, long maxOffset, int max)
            {
                Offset = offset;
                MaxOffset = maxOffset;
                Max = max;
            }
        }
        public Source<Try<(IPersistentRepresentation, IImmutableSet<string>, long)>, NotUsed> Events(
            long offset,
            long maxOffset,
            long max)
        {
            var maxTake = MaxTake(max);

            return AsyncSource<JournalRow>.FromEnumerable(
                new { _connectionFactory = ConnectionFactory, args=new QueryArgs(offset,maxOffset,maxTake) },
                async input =>
                {
                    var cf = input._connectionFactory;
                    var a = input.args;
                    return await ExecuteEventQuery(a);
                }
            ).Via(_deserializeFlow);
        }

        internal async Task<List<Try<(IPersistentRepresentation, IImmutableSet<string>, long)>>> QueryEvents(long offset, long maxOffset, long take)
        {
            var e = await ExecuteEventQuery(offset,maxOffset, take);
            var retSet = new List<Try<(IPersistentRepresentation, IImmutableSet<string>, long)>>(e.Count);
            foreach (var item in e)
            {
                retSet.Add(_deserializer.Deserialize(item));
            }

            return retSet;
        }

        internal async Task<List<JournalRow>> ExecuteEventQuery(long offset, long maxOffset, long take)
        {
            return await ExecuteEventQuery(new QueryArgs(offset, maxOffset, MaxTake(take)));
        }
        internal async Task<List<JournalRow>> ExecuteEventQuery(QueryArgs queryArgs)
        {
            return await ConnectionFactory.ExecuteWithTransactionAsync(
                queryArgs,
                ReadIsolationLevel,
                ShutdownToken,
                async (connection, token,a) =>
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

                    return await AddTagDataIfNeededAsync(query, connection, token);
                });
        }

        private async Task<List<JournalRow>> AddTagDataIfNeededAsync(IQueryable<JournalRow> rowQuery, AkkaDataConnection connection, CancellationToken token)
        {
            if (_readJournalConfig.PluginConfig.TagMode != TagMode.TagTable)
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
