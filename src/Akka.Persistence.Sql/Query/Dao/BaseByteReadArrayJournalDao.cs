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
using LinqToDB.Tools;

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
            FlowPersistentReprSerializer<JournalRow> serializer)
            : base(scheduler, materializer, connectionFactory)
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
                    await using var connection = input._connectionFactory.GetConnection();

                    return await connection
                        .GetTable<JournalRow>()
                        .Where(r => r.Deleted == false)
                        .Select(r => r.PersistenceId)
                        .Distinct()
                        .Take(input.maxTake)
                        .ToListAsync();
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
                        new { separator, tag, offset, maxOffset, maxTake, ConnectionFactory },
                        async input =>
                        {
                            await using var connection = input.ConnectionFactory.GetConnection();

                            var tagValue = $"{separator}{input.tag}{separator}";

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
                                .ToListAsync();
                        })
                    .Via(_deserializeFlow),

                TagMode.TagTable => AsyncSource<JournalRow>
                    .FromEnumerable(
                        new { separator, tag, offset, maxOffset, maxTake, ConnectionFactory },
                        async input =>
                        {
                            await using var connection = input.ConnectionFactory.GetConnection();

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

                            var mainRows = await query.ToListAsync();

                            await AddTagDataFromTagTable(mainRows, connection);

                            return mainRows;
                        })
                    .Via(_deserializeFlow),

                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public override Source<Try<ReplayCompletion>, NotUsed> Messages(
            AkkaDataConnection connection,
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            long max)
            => AsyncSource<JournalRow>
                .FromEnumerable(
                    new { connection, persistenceId, fromSequenceNr, toSequenceNr, toTake = MaxTake(max) },
                    async state =>
                    {
                        var mainRows = await connection
                            .GetTable<JournalRow>()
                            .Where(
                                r =>
                                    r.PersistenceId == state.persistenceId &&
                                    r.SequenceNumber >= state.fromSequenceNr &&
                                    r.SequenceNumber <= state.toSequenceNr &&
                                    r.Deleted == false)
                            .OrderBy(r => r.SequenceNumber)
                            .Take(state.toTake)
                            .ToListAsync();

                        if (_readJournalConfig.PluginConfig.TagMode == TagMode.TagTable)
                            await AddTagDataFromTagTable(mainRows, connection);

                        return mainRows;
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
                    });

        public Source<long, NotUsed> JournalSequence(long offset, long limit)
        {
            var maxTake = MaxTake(limit);

            return AsyncSource<long>.FromEnumerable(
                new { maxTake, offset, _connectionFactory = ConnectionFactory },
                async input =>
                {
                    await using var connection = input._connectionFactory.GetConnection();

                    // persistence-jdbc does not filter deleted here.
                    return await connection
                        .GetTable<JournalRow>()
                        .Where(r => r.Ordering > input.offset)
                        .Select(r => r.Ordering)
                        .OrderBy(r => r)
                        .Take(input.maxTake)
                        .ToListAsync();
                });
        }

        public async Task<long> MaxJournalSequenceAsync()
        {
            await using var connection = ConnectionFactory.GetConnection();

            // persistence-jdbc does not filter deleted here.
            var result = await connection
                .GetTable<JournalRow>()
                .MaxAsync<JournalRow, long?>(r => r.Ordering);
            return result ?? 0;
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
                    await using var connection = input._connectionFactory.GetConnection();

                    var events = await connection
                        .GetTable<JournalRow>()
                        .Where(
                            r =>
                                r.Ordering > input.offset &&
                                r.Ordering <= input.maxOffset &&
                                r.Deleted == false)
                        .OrderBy(r => r.Ordering)
                        .Take(input.maxTake)
                        .ToListAsync();

                    return await AddTagDataIfNeeded(events, connection);
                }
            ).Via(_deserializeFlow);
        }

        private async Task<List<JournalRow>> AddTagDataIfNeeded(List<JournalRow> toAdd, AkkaDataConnection connection)
        {
            if (_readJournalConfig.PluginConfig.TagMode == TagMode.TagTable)
                await AddTagDataFromTagTable(toAdd, connection);

            return toAdd;
        }

        private static async Task AddTagDataFromTagTable(List<JournalRow> toAdd, AkkaDataConnection connection)
        {
            if (toAdd.Count == 0)
                return;

            var tagRows = await connection
                .GetTable<JournalTagRow>()
                .Where(r => r.OrderingId.In(toAdd.Select(row => row.Ordering).Distinct()))
                .Select(
                    r => new TagRow
                    {
                        OrderingId = r.OrderingId,
                        TagValue = r.TagValue
                    })
                .ToListAsync();

            foreach (var journalRow in toAdd)
            {
                journalRow.TagArr = tagRows
                    .Where(r => r.OrderingId == journalRow.Ordering)
                    .Select(r => r.TagValue)
                    .ToArray();
            }
        }
    }
}
