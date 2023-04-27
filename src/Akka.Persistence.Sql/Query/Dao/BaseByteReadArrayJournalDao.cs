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
            FlowPersistentReprSerializer<JournalRow> serializer,
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
                    return await input._connectionFactory.ExecuteWithTransactionAsync(
                        ReadIsolationLevel,
                        ShutdownToken,
                        async (connection, token) =>
                        {
                            return await connection
                                .GetTable<JournalRow>()
                                .Where(r => r.Deleted == false)
                                .Select(r => r.PersistenceId)
                                .Distinct()
                                .Take(input.maxTake)
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
                        new { separator, tag, offset, maxOffset, maxTake, _connectionFactory = ConnectionFactory },
                        async input =>
                        {
                            var tagValue = $"{separator}{input.tag}{separator}";
                            return await input._connectionFactory.ExecuteWithTransactionAsync(
                                ReadIsolationLevel,
                                ShutdownToken,
                                async (connection, token) =>
                                {
                                    return await connection
                                        .GetTable<JournalRow>()
                                        .Where(
                                            r =>
                                                r.Tags.Contains(tagValue) &&
                                                !r.Deleted &&
                                                r.Ordering > input.offset &&
                                                r.Ordering <= input.maxOffset)
                                        .OrderBy(r => r.Ordering)
                                        .Take(input.maxTake)
                                        .ToListAsync(token);
                                });
                        })
                    .Via(_deserializeFlow),

                TagMode.TagTable => AsyncSource<JournalRow>
                    .FromEnumerable(
                        new { separator, tag, offset, maxOffset, maxTake, _connectionFactory = ConnectionFactory },
                        async input =>
                        {
                            return await input._connectionFactory.ExecuteWithTransactionAsync(
                                ReadIsolationLevel,
                                ShutdownToken,
                                async (connection, token) =>
                                {
                                    var journalTable = connection.GetTable<JournalRow>();
                                    var tagTable = connection.GetTable<JournalTagRow>();

                                    var query =
                                        from r in journalTable
                                        from lp in tagTable.Where(jtr => jtr.OrderingId == r.Ordering).DefaultIfEmpty()
                                        where lp.OrderingId > input.offset &&
                                              lp.OrderingId <= input.maxOffset &&
                                              !r.Deleted &&
                                              lp.TagValue == input.tag
                                        orderby r.Ordering
                                        select r;

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
                                ReadIsolationLevel,
                                ShutdownToken,
                                async (connection, token) =>
                                {
                                    var query = connection
                                        .GetTable<JournalRow>()
                                        .Where(
                                            r =>
                                                r.PersistenceId == state.persistenceId &&
                                                r.SequenceNumber >= state.fromSequenceNr &&
                                                r.SequenceNumber <= state.toSequenceNr &&
                                                r.Deleted == false)
                                        .OrderBy(r => r.SequenceNumber)
                                        .Take(state.toTake);

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

        public Source<Try<(IPersistentRepresentation, IImmutableSet<string>, long)>, NotUsed> Events(
            long offset,
            long maxOffset,
            long max)
        {
            var maxTake = MaxTake(max);

            return AsyncSource<JournalRow>.FromEnumerable(
                new { _connectionFactory = ConnectionFactory, maxTake, maxOffset, offset },
                async input =>
                {
                    return await input._connectionFactory.ExecuteWithTransactionAsync(
                        ReadIsolationLevel,
                        ShutdownToken,
                        async (connection, token) =>
                        {
                            var query = connection
                                .GetTable<JournalRow>()
                                .Where(
                                    r =>
                                        r.Ordering > input.offset &&
                                        r.Ordering <= input.maxOffset &&
                                        r.Deleted == false)
                                .OrderBy(r => r.Ordering)
                                .Take(input.maxTake);

                            return await AddTagDataIfNeededAsync(query, connection, token);
                        });
                }
            ).Via(_deserializeFlow);
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
            var q =
                from jr in rowQuery
                select new
                {
                    row = jr,
                    tags = tagTable
                        .Where(r => r.OrderingId == jr.Ordering)
                        .StringAggregate(";", r => r.TagValue)
                        .ToValue(),
                };

            var res = await q.ToListAsync(token);
            var result = new List<JournalRow>();
            foreach (var row in res)
            {
                row.row.TagArr = row.tags?.Split(';') ?? Array.Empty<string>();
                result.Add(row.row);
            }

            return result;
        }
    }
}
