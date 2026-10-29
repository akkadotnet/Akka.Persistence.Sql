// -----------------------------------------------------------------------
//  <copyright file="BaseJournalDaoWithReadMessages.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Utility;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;

namespace Akka.Persistence.Sql.Journal.Dao
{
    public abstract class BaseJournalDaoWithReadMessages : IJournalDaoWithReadMessages
    {
        protected readonly AkkaPersistenceDataConnectionFactory ConnectionFactory;

        protected readonly IMaterializer Materializer;

        protected readonly IsolationLevel ReadIsolationLevel;

        protected readonly IAdvancedScheduler Scheduler;

        protected readonly CancellationToken ShutdownToken;

        protected readonly IsolationLevel WriteIsolationLevel;

        protected BaseJournalDaoWithReadMessages(
            IAdvancedScheduler scheduler,
            IMaterializer materializer,
            AkkaPersistenceDataConnectionFactory connectionFactory,
            IProviderConfig config,
            CancellationToken shutdownToken)
        {
            Scheduler = scheduler;
            Materializer = materializer;
            ConnectionFactory = connectionFactory;
            ReadIsolationLevel = config.ReadIsolationLevel;
            WriteIsolationLevel = config.WriteIsolationLevel;
            ShutdownToken = shutdownToken;
        }

        public abstract Task<Source<Try<ReplayCompletion>, NotUsed>> Messages(
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            long max);

        public Source<Try<ReplayCompletion>, NotUsed> MessagesWithBatch(
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            int batchSize,
            Option<(TimeSpan duration, IScheduler scheduler)> refreshInterval)
            => Source.From(
                () => MessagesWithBatchAsync(persistenceId, fromSequenceNr, toSequenceNr, batchSize, refreshInterval));

        // The token is cancelled when the stream stops, which cancels the in-flight batch query.
        private async IAsyncEnumerable<Try<ReplayCompletion>> MessagesWithBatchAsync(
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            int batchSize,
            Option<(TimeSpan duration, IScheduler scheduler)> refreshInterval,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ShutdownToken);
            var token = cts.Token;

            var seqNr = Math.Max(1, fromSequenceNr);
            while (true)
            {
                var batch = new List<Try<ReplayCompletion>>();
                var source = await Messages(persistenceId, seqNr, toSequenceNr, batchSize);
                await foreach (var message in source.RunAsAsyncEnumerable(Materializer).WithCancellation(token))
                    batch.Add(message);

                // Cancellation completes the batch stream early: never mistake that for a short batch.
                token.ThrowIfCancellationRequested();

                var lastSeq = Option<long>.None;
                if (batch.Count > 0)
                    lastSeq = batch[batch.Count - 1].Get().Representation.SequenceNr;

                foreach (var message in batch)
                    yield return message;

                if ((lastSeq.HasValue && lastSeq.Value >= toSequenceNr) || seqNr > toSequenceNr)
                    yield break;

                if (lastSeq.HasValue)
                    seqNr = lastSeq.Value + 1;

                if (batch.Count == batchSize)
                    continue;

                if (!refreshInterval.HasValue)
                    yield break;

                await QueryDelay.WaitAsync(refreshInterval.Value.duration, refreshInterval.Value.scheduler, token);
            }
        }
    }
}
