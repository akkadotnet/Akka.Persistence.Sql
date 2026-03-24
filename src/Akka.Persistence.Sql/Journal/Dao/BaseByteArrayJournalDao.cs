// -----------------------------------------------------------------------
//  <copyright file="BaseByteArrayJournalDao.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Extensions;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Serialization;
using Akka.Persistence.Sql.Utility;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Streams.Supervision;
using LanguageExt;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Data;
using LinqToDB.Internal.DataProvider.Oracle;
using LinqToDB.Internal.DataProvider.PostgreSQL;
using LinqToDB.Internal.DataProvider.SQLite;
using LinqToDB.Internal.DataProvider.SqlServer;
using static LanguageExt.Prelude;

namespace Akka.Persistence.Sql.Journal.Dao
{
    public abstract class BaseByteArrayJournalDao : BaseJournalDaoWithReadMessages, IJournalDaoWithUpdates
    {
        private readonly Flow<JournalRow, Util.Try<ReplayCompletion>, NotUsed> _deserializeFlowMapped;
        private readonly TagMode _tagWriteMode;
        private readonly bool _useTagTableAsQueryable;
        protected readonly JournalConfig JournalConfig;

        protected readonly ILoggingAdapter Logger;
        protected readonly FlowPersistentRepresentationSerializer<JournalRow> Serializer;

        /// <summary>
        /// Bounded input channel replacing the old <c>Source.Queue</c>.
        /// Uses <see cref="BoundedChannelFullMode.Wait"/> for natural backpressure
        /// instead of <c>OverflowStrategy.DropNew</c> (which silently dropped writes). 🌸
        ///
        /// <para>
        /// <b>CopilotNote:</b> The writer side (<c>_inputChannel.Writer</c>) is used in
        /// <see cref="QueueWriteJournalRows"/> to enqueue entries. The reader side is
        /// consumed by <see cref="_batcher"/>.
        /// </para>
        /// </summary>
        private readonly Channel<WriteQueueEntry> _inputChannel;

        /// <summary>
        /// Weighted batcher that wraps <see cref="_inputChannel"/>'s reader and eagerly
        /// aggregates <see cref="WriteQueueEntry"/> items into <see cref="WriteQueueSet"/>
        /// batches up to <c>BatchSize</c> total row cost. This IS a
        /// <see cref="ChannelReader{WriteQueueSet}"/> — it feeds directly into
        /// <c>Source.ChannelReader</c> for downstream Akka Streams processing. ✨
        ///
        /// <para>
        /// <b>CopilotNote:</b> Replaces the old <c>BatchWeighted</c> Akka Streams stage
        /// with pure channel-based batching via <see cref="ChannelQueueWithBatch{TInput,TBatch}"/>.
        /// </para>
        /// </summary>
        private readonly ChannelQueueWithBatch<WriteQueueEntry, WriteQueueSet> _batcher;

        protected BaseByteArrayJournalDao(
            IAdvancedScheduler scheduler,
            IMaterializer materializer,
            AkkaPersistenceDataConnectionFactory connectionFactory,
            JournalConfig config,
            Akka.Serialization.Serialization serializer,
            ILoggingAdapter logger,
            string? selfUuid,
            CancellationToken shutdownToken)
            : base(scheduler, materializer, connectionFactory, config, shutdownToken)
        {
            Logger = logger;
            JournalConfig = config;
            Serializer = new ByteArrayJournalSerializer(config, serializer, config.PluginConfig.TagSeparator, selfUuid);
            _deserializeFlowMapped = Serializer.DeserializeFlow().Select(MessageWithBatchMapper());
            _tagWriteMode = JournalConfig.PluginConfig.TagMode;
            _useTagTableAsQueryable = (JournalConfig.DaoConfig.UseTagTableAsQueryableLiteralInsert
                                       &&
                                       (JournalConfig.ProviderName.Contains("SqlServer") || JournalConfig.ProviderName.Contains("PostgreSQL") ||
                                        JournalConfig.ProviderName.Contains("Sqlite")));
            // CopilotNote: Phase 2 — Channel + ChannelQueueWithBatch replaces
            // Source.Queue + BatchWeighted. The input channel provides backpressure via
            // BoundedChannelFullMode.Wait instead of dropping writes with DropNew. 🌸
            _inputChannel = Channel.CreateBounded<WriteQueueEntry>(
                new BoundedChannelOptions(JournalConfig.DaoConfig.BufferSize)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = true,
                });

            _batcher = new ChannelQueueWithBatch<WriteQueueEntry, WriteQueueSet>(
                _inputChannel.Reader,
                maxWeight: JournalConfig.DaoConfig.BatchSize,
                costFunction: entry => entry.Rows.Count,
                seed: r => new WriteQueueSet(
                    ImmutableList.Create([r.Tcs]),
                    r.Rows,
                    ImmutableList.Create([r.CancellationToken])),
                aggregate: (oldRows, newRows) =>
                    new WriteQueueSet(
                        oldRows.Tcs.Add(newRows.Tcs),
                        oldRows.Rows.Concat(newRows.Rows),
                        oldRows.CancellationTokens.Add(newRows.CancellationToken)));

            // The batcher IS a ChannelReader<WriteQueueSet> — pipe it into Akka Streams ✨
            Source.ChannelReader(_batcher)
                .Async()
                .SelectAsync(
                    JournalConfig.DaoConfig.Parallelism,
                    async promisesAndRows =>
                    {
                        if (promisesAndRows.Rows.Length > 1)
                        {
                            logger.Error("Writing journal rows in parallel, total rows {prows}", promisesAndRows.Rows.Length);
                        }
                        try
                        {
                            await WriteJournalRows(promisesAndRows.Rows, promisesAndRows.CancellationTokens);
                            foreach (var taskCompletionSource in promisesAndRows.Tcs)
                                taskCompletionSource.TrySetResult(NotUsed.Instance);
                        }
                        catch (Exception e)
                        {
                            foreach (var taskCompletionSource in promisesAndRows.Tcs)
                                taskCompletionSource.TrySetException(e);
                        }

                        return NotUsed.Instance;
                    })
                .AddAttributes(ActorAttributes.CreateSupervisionStrategy(Deciders.RestartingDecider))
                .To(Sink.Ignore<NotUsed>())
                .Run(Materializer);
        }

        public async Task<IImmutableList<Exception>> AsyncWriteMessages(
            IEnumerable<AtomicWrite> messages,
            CancellationToken cancellationToken,
            long timeStamp = 0)
        {
            var serializedTries = Serializer.Serialize(messages, timeStamp);

            // Fold our List of Lists into a single sequence
            var rows = Seq(FlattenListOfListsToList(serializedTries));

            // Wait for the write to go through. If Task fails, write will be captured as WriteMessagesFailure.
            await QueueWriteJournalRows(rows, cancellationToken);

            // If we get here, we build an ImmutableList containing our rejections.
            // These will be captured as WriteMessagesRejected
            return BuildWriteRejections(serializedTries);
        }

        public async Task Delete(string persistenceId, long maxSequenceNr, CancellationToken cancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ShutdownToken);
            long maxMarkedDeletion;

            // no need to use transaction
            await using (var connection = ConnectionFactory.GetConnection())
            {
                maxMarkedDeletion = await MaxMarkedForDeletionMaxPersistenceIdQuery(connection, persistenceId, maxSequenceNr).FirstOrDefaultAsync(cts.Token);
            }

            if (maxMarkedDeletion is 0)
                return;
            
            await ConnectionFactory.ExecuteWithTransactionAsync(
                WriteIsolationLevel,
                cts.Token,
                async (connection, token) =>
                {
                    var journalTable = connection.GetTable<JournalRow>();
                    await journalTable
                        .Where(
                            r =>
                                r.PersistenceId == persistenceId &&
                                r.SequenceNumber == maxMarkedDeletion)
                        .Set(r => r.Deleted, true)
                        .UpdateAsync(token);

                    await journalTable
                        .Where(
                            r =>
                                r.PersistenceId == persistenceId &&
                                r.SequenceNumber < maxMarkedDeletion)
                        .DeleteAsync(token);

                    if (JournalConfig.DaoConfig.SqlCommonCompatibilityMode)
                    {
                        await connection
                            .GetTable<JournalMetaData>()
                            .InsertOrUpdateAsync(
                                insertSetter: () => new JournalMetaData
                                {
                                    PersistenceId = persistenceId,
                                    SequenceNumber = maxMarkedDeletion,
                                },
                                onDuplicateKeyUpdateSetter: jmd => new JournalMetaData(),
                                keySelector: () => new JournalMetaData
                                {
                                    PersistenceId = persistenceId,
                                    SequenceNumber = maxMarkedDeletion,
                                },
                                token: token);

                        await connection
                            .GetTable<JournalMetaData>()
                            .Where(
                                r =>
                                    r.PersistenceId == persistenceId &&
                                    r.SequenceNumber < maxMarkedDeletion)
                            .DeleteAsync(token);
                    }

                    if (JournalConfig.PluginConfig.TagMode != TagMode.Csv)
                    {
                        await connection
                            .GetTable<JournalTagRow>()
                            .Where(
                                r =>
                                    r.SequenceNumber < maxMarkedDeletion &&
                                    r.PersistenceId == persistenceId)
                            .DeleteAsync(token);
                    }
                });
        }

        public async Task<Done> Update(string persistenceId, long sequenceNr, object payload)
        {
            var write = new Persistent(payload, sequenceNr, persistenceId);
            var serialize = Serializer.Serialize(write);

            if (!serialize.IsSuccess)
            {
                throw new ArgumentException(
                    $"Failed to serialize {write.GetType()} for update of {persistenceId}] @ {sequenceNr}",
                    serialize.Failure.Value);
            }

            await ConnectionFactory.ExecuteWithTransactionAsync(
                WriteIsolationLevel,
                ShutdownToken,
                async (connection, token) =>
                {
                    await connection
                        .GetTable<JournalRow>()
                        .Where(
                            r =>
                                r.PersistenceId == persistenceId &&
                                r.SequenceNumber == write.SequenceNr)
                        .Set(r => r.Message, serialize.Get().Message)
                        .UpdateAsync(token);
                });

            return Done.Instance;
        }

        public async Task<long> HighestSequenceNr(string persistenceId, long fromSequenceNr, CancellationToken cancellationToken)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ShutdownToken);
            return await ConnectionFactory.ExecuteWithTransactionAsync(
                ReadIsolationLevel,
                cts.Token,
                async (connection, token) => (await MaxSeqNumberForPersistenceIdQuery(connection, persistenceId, fromSequenceNr).MaxAsync(token))
                    .GetValueOrDefault(0));
        }

        /// <summary>
        ///     This override is greedy since it is always called
        ///     from within <see cref="BaseJournalDaoWithReadMessages.MessagesWithBatch" />
        /// </summary>
        /// <param name="persistenceId"></param>
        /// <param name="fromSequenceNr"></param>
        /// <param name="toSequenceNr"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public override async Task<Source<Util.Try<ReplayCompletion>, NotUsed>> Messages(
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            long max)
        {
            return await ConnectionFactory.ExecuteWithTransactionAsync(
                ReadIsolationLevel,
                ShutdownToken,
                async (connection, token) =>
                {
                    IQueryable<JournalRow> query = connection
                        .GetTable<JournalRow>()
                        .Where(
                            r =>
                                r.PersistenceId == persistenceId &&
                                r.SequenceNumber >= fromSequenceNr &&
                                r.SequenceNumber <= toSequenceNr &&
                                r.Deleted == false)
                        .OrderBy(r => r.SequenceNumber);

                    if (max <= int.MaxValue)
                        query = query.Take((int)max);

                    var source = await query.ToListAsync(token);

                    return Source
                        .From(source)
                        .Via(_deserializeFlowMapped);
                });
        }

        /// <summary>
        /// Enqueues a set of journal rows for batched writing via the channel pipeline.
        /// Uses <c>TryWrite</c> on the bounded input channel — returns <c>false</c> when
        /// the channel is full or completed, replacing the old <c>QueueOfferResult</c>
        /// switch statement with simpler channel semantics. uwu 🌸
        ///
        /// <para>
        /// <b>CopilotNote:</b> With <see cref="BoundedChannelFullMode.Wait"/>,
        /// <c>TryWrite</c> is non-blocking and returns <c>false</c> when:
        /// <list type="bullet">
        ///   <item>Channel is at capacity (equivalent to old <c>Dropped</c>)</item>
        ///   <item>Channel was completed with error (equivalent to old <c>Failure</c>)</item>
        ///   <item>Channel was completed normally (equivalent to old <c>QueueClosed</c>)</item>
        /// </list>
        /// We distinguish these cases via <c>_batcher.Completion</c>.
        /// </para>
        /// </summary>
        private async Task QueueWriteJournalRows(Seq<JournalRow> xs, CancellationToken cancellationToken)
        {
            var promise = new TaskCompletionSource<NotUsed>(TaskCreationOptions.RunContinuationsAsynchronously);

            // TryWrite is non-blocking. With FullMode=Wait it returns false when the
            // channel is at capacity or completed — never silently drops items. ✨
            if (!_inputChannel.Writer.TryWrite(new WriteQueueEntry(promise, xs, cancellationToken)))
            {
                // Distinguish full vs faulted vs closed — mirrors original QueueOfferResult cases
                var completionException = _batcher.Completion.Exception;
                if (completionException is not null)
                    promise.TrySetException(new Exception("Failed to write journal row batch", completionException));
                else if (_batcher.Completion.IsCompleted)
                    promise.TrySetException(
                        new Exception(
                            "Failed to enqueue journal row batch write, the queue was closed."));
                else
                    promise.TrySetException(
                        new Exception(
                            $"Failed to enqueue journal row batch write, the queue buffer was full ({JournalConfig.DaoConfig.BufferSize} elements)"));
            }

            await promise.Task.ConfigureAwait(false);
        }

        private async Task WriteJournalRows(Seq<JournalRow> xs, ImmutableList<CancellationToken> cancellationTokens)
        {
            switch (xs.Count)
            {
                case 0:
                    break;

                // hot path:
                // If we only have one row, penalty for BulkCopy
                // Isn't worth it due to insert caching/etc.
                case 1 when _tagWriteMode == TagMode.Csv || xs.Head().TagArray.Length == 0:
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ShutdownToken, cancellationTokens[0]);
                    await ConnectionFactory.ExecuteWithTransactionAsync(
                        WriteIsolationLevel,
                        cts.Token,
                        async (connection, token) => await connection.InsertAsync(xs.Head, token));
                    break;
                }

                default:
                    await InsertMultiple(xs, cancellationTokens);
                    break;
            }
        }

        private async Task InsertMultiple(Seq<JournalRow> xs, ImmutableList<CancellationToken> cancellationTokens)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokens.Add(ShutdownToken).ToArray());
            await ConnectionFactory.ExecuteWithTransactionAsync(
                WriteIsolationLevel,
                cts.Token,
                async (connection, token) =>
                {
                    if (_tagWriteMode == TagMode.Csv)
                    {
                        await BulkInsertNoTagTableTags(connection, xs, JournalConfig.DaoConfig, token);
                    }
                    else
                    {
                        if (_useTagTableAsQueryable)
                        {
                            await RunFastInsertNoEventParams(connection, xs, JournalConfig.DaoConfig, token);
                        }
                        else
                        {
                            var config = JournalConfig.DaoConfig;
                            var tail = xs;
                            while (tail.Count > 0)
                            {
                                (var noTags, tail) = tail.Span(r => r.TagArray.Length == 0);
                                if (noTags.Count > 0)
                                    await BulkInsertNoTagTableTags(connection, noTags, config, token);

                                (var hasTags, tail) = tail.Span(r => r.TagArray.Length > 0);
                                if (hasTags.Count > 0)
                                    await InsertWithOrderingAndBulkInsertTags(connection, hasTags, config, token);
                            }
                        }
                    }
                });
        }

        public class JournalRowIns
        {
            public string PersistenceId { get; set; }
            public long SequenceNumber { get; set; }
            public byte[] Message { get; set; }
            public bool Deleted { get; set; }
            public string Manifest { get; set; }
            public long Timestamp { get; set; }
            public int? Identifier { get; set; }
            public string? WriterUuid { get; set; }
        }

        protected async Task RunFastInsertNoEventParams(AkkaDataConnection connection, Seq<JournalRow> xs, BaseByteArrayJournalDaoConfig journalConfigDaoConfig, CancellationToken token)
        {
            
            var roundTripByteLimit = JournalConfig.DaoConfig.AsQueryableInsertSqlLengthLimit;
            var rowLimit = JournalConfig.DaoConfig.BatchSize; // IDK
            var currRows = 0;
            var currBytes = 0;
            var thisInsTagSize = 0;
            Dictionary<(string PersistenceId, long SequenceNumber), string[]> tagDict 
                = new Dictionary<(string persistenceId, long sequenceNumber), string[]>();
            var insertList = new List<JournalRow>(rowLimit);
            foreach (var journalRow in xs)
            {
                // We don't worry about adding 1024 on this check cause it's overparanoid padding anyway.
                if (journalRow.Message.Length + currBytes * 2 > roundTripByteLimit || currRows >= rowLimit)
                {
                    var query = connection.AsQueryable(
                        insertList.Select(jr => new JournalRowIns
                        {
                            PersistenceId = jr.PersistenceId,
                            SequenceNumber = jr.SequenceNumber,
                            Message = jr.Message,
                            Deleted = jr.Deleted,
                            Manifest = jr.Manifest,
                            Timestamp = jr.Timestamp,
                            Identifier = jr.Identifier,
                            WriterUuid = jr.WriterUuid
                        }));
                    await InsertJournalEntriesWithTags(connection, journalConfigDaoConfig, token, tagDict, query, thisInsTagSize);
                    insertList.Clear();
                    tagDict.Clear();
                    currRows = 0;
                    currBytes = 0;
                    thisInsTagSize = 0;
                }

                tagDict[(journalRow.PersistenceId, journalRow.SequenceNumber)]
                    = journalRow.TagArray;
                thisInsTagSize = thisInsTagSize + journalRow.TagArray.Length;
                currRows++;
                // ByteLength *2
                //  + 2048 for padding
                // (i.e. Serializer manifests, persistence IDs, sequence numbers etc.)
                currBytes +=
                    (journalRow.Message.Length * 2 + 2048);
                insertList.Add(journalRow);

            }

            if (currBytes > 0 || currRows > 0)
            {
                var query = connection.AsQueryable(
                    insertList.Select(jr => new JournalRowIns
                    {
                        PersistenceId = jr.PersistenceId,
                        SequenceNumber = jr.SequenceNumber,
                        Message = jr.Message,
                        Deleted = jr.Deleted,
                        Manifest = jr.Manifest,
                        Timestamp = jr.Timestamp,
                        Identifier = jr.Identifier,
                        WriterUuid = jr.WriterUuid
                    }));
                await InsertJournalEntriesWithTags(connection, journalConfigDaoConfig, token, tagDict, query, thisInsTagSize);
            }
        }


        private async Task InsertJournalEntriesWithTags(
            AkkaDataConnection connection,
            BaseByteArrayJournalDaoConfig journalConfigDaoConfig,
            CancellationToken token,
            Dictionary<(string PersistenceId, long SequenceNumber), string[]> tagDict,
            IQueryable<JournalRowIns> query,
            int thisInsTagSize)
        {
            var inserted = await (this.JournalConfig.TableConfig.EventJournalTable.UseWriterUuidColumn
                ? //query.InsertWithOutputAsync(
                    query.InsertWithOutputListAsync(    
                        connection.GetTable<JournalRow>(),
                        (input) =>
                            new JournalRow()
                            {
                                PersistenceId = input.PersistenceId,
                                SequenceNumber = input.SequenceNumber,
                                Message = input.Message,
                                Deleted = input.Deleted,
                                Manifest = input.Manifest,
                                Timestamp = input.Timestamp,
                                Identifier = input.Identifier,
                                WriterUuid = input.WriterUuid,
                            },
                        (inserted) => new { inserted.Ordering, inserted.PersistenceId, inserted.SequenceNumber })
                    //.ToListAsync(token)
                
                : //query.InsertWithOutputAsync(
                query.InsertWithOutputListAsync(
                        connection.GetTable<JournalRow>(),
                        (input) =>
                            new JournalRow()
                            {
                                PersistenceId = input.PersistenceId,
                                SequenceNumber = input.SequenceNumber,
                                Message = input.Message,
                                Deleted = input.Deleted,
                                Manifest = input.Manifest,
                                Timestamp = input.Timestamp,
                                Identifier = input.Identifier,
                            },
                        (inserted) => new { inserted.Ordering, inserted.PersistenceId, inserted.SequenceNumber })
                    // .ToListAsync(token)
                );
            var insertList = new List<JournalTagRow>(thisInsTagSize);
            foreach (var ir in inserted)
            {
                if (tagDict.TryGetValue((ir.PersistenceId, ir.SequenceNumber), out var tags))
                {
                    insertList.AddRange(tags.Select(t => new JournalTagRow()
                    {
                        OrderingId = ir.Ordering,
                        PersistenceId = ir.PersistenceId,
                        SequenceNumber = ir.SequenceNumber,
                        TagValue = t
                    }));
                }
            }

            var tagsToInsert =
                insertList;
                // inserted.Join(
                //     tagDict,
                //     i => (i.PersistenceId, i.SequenceNumber),
                //     d => d.Key,
                //     (i, d) =>
                //         d.Value.Select(t => new JournalTagRow()
                //                 { OrderingId = i.Ordering, PersistenceId = i.PersistenceId, SequenceNumber = i.SequenceNumber, TagValue = t })
                //             //.ToList()
                // )
                // .ToList();
                    
            await connection.GetTable<JournalTagRow>()
                .BulkCopyAsync(
                    new BulkCopyOptions()
                        .WithBulkCopyType(BulkCopyType.MultipleRows)
                        .WithUseParameters(journalConfigDaoConfig.PreferParametersOnMultiRowInsert)
                        .WithMaxBatchSize(journalConfigDaoConfig.DbRoundTripTagBatchSize),
                    tagsToInsert
                    ,
                        //.SelectMany(t => t),
                    token);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async Task InsertWithOrderingAndBulkInsertTags(
            AkkaDataConnection connection,
            Seq<JournalRow> xs,
            BaseByteArrayJournalDaoConfig config,
            CancellationToken token)
        {
            var tagsToInsert = new List<JournalTagRow>(xs.Count);

            // We could not do bulk copy and retrieve the inserted ids
            // Issue: https://github.com/linq2db/linq2db/issues/2960
            // We're forced to insert the rows one by one.
            foreach (var journalRow in xs)
            {
                var dbId = await connection.InsertWithInt64IdentityAsync(journalRow, token);

                tagsToInsert.AddRange(
                    journalRow.TagArray.Select(
                        s1 => new JournalTagRow
                        {
                            OrderingId = dbId,
                            TagValue = s1,
                            PersistenceId = journalRow.PersistenceId,
                            SequenceNumber = journalRow.SequenceNumber,
                        }));
            }

            await connection
                .GetTable<JournalTagRow>()
                .BulkCopyAsync(
                    new BulkCopyOptions()
                        .WithBulkCopyType(BulkCopyType.MultipleRows)
                        .WithUseParameters(config.PreferParametersOnMultiRowInsert)
                        .WithMaxBatchSize(config.DbRoundTripTagBatchSize),
                    tagsToInsert,
                    token);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async Task BulkInsertNoTagTableTags(
            AkkaDataConnection connection,
            Seq<JournalRow> xs,
            BaseByteArrayJournalDaoConfig config,
            CancellationToken token)
            => await connection
                .GetTable<JournalRow>()
                .BulkCopyAsync(
                    new BulkCopyOptions()
                        .WithBulkCopyType(
                            xs.Count > config.MaxRowByRowSize
                                ? BulkCopyType.Default
                                : BulkCopyType.MultipleRows)
                        .WithUseParameters(config.PreferParametersOnMultiRowInsert)
                        .WithMaxBatchSize(config.DbRoundTripBatchSize),
                    xs,
                    token);

        // By using a custom flatten here, we avoid an Enumerable/LINQ allocation
        // And are able to have a little more control over default capacity of array.
        private static IEnumerable<JournalRow> FlattenListOfListsToList(List<Util.Try<JournalRow[]>> source)
        {
            var rows = new List<JournalRow>(
                source.Count > 4
                    ? source.Count
                    : 4);

            foreach (var t in source)
            {
                var item = t.Success.Value;
                if (item is not null)
                    rows.AddRange(item);
            }

            return rows;
        }

        protected static ImmutableList<Exception> BuildWriteRejections(List<Util.Try<JournalRow[]>> serializedTries)
        {
            var builderEx = new Exception[serializedTries.Count];
            for (var i = 0; i < serializedTries.Count; i++)
                builderEx[i] = serializedTries[i].Failure.Value;

            return ImmutableList.CreateRange(builderEx);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IQueryable<long> MaxMarkedForDeletionMaxPersistenceIdQuery(
            AkkaDataConnection connection,
            string persistenceId, 
            long maxSequenceNr)
            => connection
                .GetTable<JournalRow>()
                .Where(r => r.PersistenceId == persistenceId && r.SequenceNumber <= maxSequenceNr)
                .OrderByDescending(r => r.SequenceNumber)
                .Select(r => r.SequenceNumber)
                .Take(1);

        private IQueryable<long?> MaxSeqNumberForPersistenceIdQuery(
            AkkaDataConnection connection,
            string persistenceId,
            long minSequenceNumber = 0)
        {
            if (minSequenceNumber != 0)
            {
                return JournalConfig.DaoConfig.SqlCommonCompatibilityMode
                    ? MaxSeqForPersistenceIdQueryableCompatibilityModeWithMinId(
                        connection,
                        persistenceId,
                        minSequenceNumber)
                    : MaxSeqForPersistenceIdQueryableNativeModeMinId(
                        connection,
                        persistenceId,
                        minSequenceNumber);
            }

            return JournalConfig.DaoConfig.SqlCommonCompatibilityMode
                ? MaxSeqForPersistenceIdQueryableCompatibilityMode(connection, persistenceId)
                : MaxSeqForPersistenceIdQueryableNativeMode(connection, persistenceId);
        }

        private static IQueryable<long?> MaxSeqForPersistenceIdQueryableNativeMode(
            AkkaDataConnection connection,
            string persistenceId)
            => connection
                .GetTable<JournalRow>()
                .Where(r => r.PersistenceId == persistenceId)
                .Select(r => (long?)r.SequenceNumber);

        private static IQueryable<long?> MaxSeqForPersistenceIdQueryableNativeModeMinId(
            AkkaDataConnection connection,
            string persistenceId,
            long minSequenceNumber)
            => connection
                .GetTable<JournalRow>()
                .Where(
                    r =>
                        r.PersistenceId == persistenceId &&
                        r.SequenceNumber > minSequenceNumber)
                .Select(r => (long?)r.SequenceNumber);

        private static IQueryable<long?> MaxSeqForPersistenceIdQueryableCompatibilityModeWithMinId(
            AkkaDataConnection connection,
            string persistenceId,
            long minSequenceNumber)
            => connection
                .GetTable<JournalRow>()
                .Where(
                    r =>
                        r.PersistenceId == persistenceId &&
                        r.SequenceNumber > minSequenceNumber)
                .Select(r => LinqToDB.Sql.Ext.Max<long?>(r.SequenceNumber).ToValue())
                .Union(
                    connection
                        .GetTable<JournalMetaData>()
                        .Where(
                            r =>
                                r.SequenceNumber > minSequenceNumber &&
                                r.PersistenceId == persistenceId)
                        .Select(r => LinqToDB.Sql.Ext.Max<long?>(r.SequenceNumber).ToValue()));

        private static IQueryable<long?> MaxSeqForPersistenceIdQueryableCompatibilityMode(
            AkkaDataConnection connection,
            string persistenceId)
            => connection
                .GetTable<JournalRow>()
                .Where(r => r.PersistenceId == persistenceId)
                .Select(r => LinqToDB.Sql.Ext.Max<long?>(r.SequenceNumber).ToValue())
                .Union(
                    connection
                        .GetTable<JournalMetaData>()
                        .Where(r => r.PersistenceId == persistenceId)
                        .Select(r => LinqToDB.Sql.Ext.Max<long?>(r.SequenceNumber).ToValue()));

        private static Func<Util.Try<(IPersistentRepresentation, string[], long)>, Util.Try<ReplayCompletion>> MessageWithBatchMapper()
            => x => x.IsSuccess
                ? new Util.Try<ReplayCompletion>(new ReplayCompletion(x.Success.Value))
                : new Util.Try<ReplayCompletion>(x.Failure.Value);
    }
}
